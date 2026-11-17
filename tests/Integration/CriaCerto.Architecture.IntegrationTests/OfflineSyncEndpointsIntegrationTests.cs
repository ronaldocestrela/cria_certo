using System.Security.Claims;
using System.Text;
using System.Text.Json;
using CriaCerto.Api.Endpoints;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.BuildingBlocks.Abstractions.Tenancy;
using CriaCerto.Modules.Breeding.Application.Contracts;
using CriaCerto.Modules.Breeding.Application.Domain;
using CriaCerto.Modules.Breeding.Application.Features.BreedingOps;
using CriaCerto.Modules.Breeding.Application.Features.Plantel;
using CriaCerto.Modules.Calving.Application.Contracts;
using CriaCerto.Modules.Growth.Application.Contracts;
using CriaCerto.Modules.Nutrition.Application.Contracts;
using CriaCerto.Modules.Nutrition.Application.Domain;
using CriaCerto.Modules.Nutrition.Application.Features.FeedingFeatures;
using CriaCerto.Modules.Nutrition.Application.Features.SiloStockFeatures;
using CriaCerto.Modules.Sanitary.Application.Contracts;
using CriaCerto.Modules.Sanitary.Application.Domain;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using Xunit;

namespace CriaCerto.Architecture.IntegrationTests;

public class OfflineSyncEndpointsIntegrationTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly ITenantContext _tenantContextMock;
    private readonly ISender _senderMock;
    private readonly ClaimsPrincipal _userClaims;

    public OfflineSyncEndpointsIntegrationTests()
    {
        _tenantContextMock = Substitute.For<ITenantContext>();
        _tenantContextMock.TenantId.Returns(_tenantId);

        _senderMock = Substitute.For<ISender>();

        var claims = new[]
        {
            new Claim("TenantId", _tenantId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
        };
        _userClaims = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    private static HttpRequest CreateHttpRequestWithBody(string body)
    {
        var httpContext = new DefaultHttpContext();
        var bytes = Encoding.UTF8.GetBytes(body);
        httpContext.Request.Body = new MemoryStream(bytes);
        httpContext.Request.ContentType = "application/json";
        httpContext.Request.ContentLength = bytes.Length;
        return httpContext.Request;
    }

    [Fact]
    public async Task HandleSyncOperation_Breeding_RegisterDiagnosis_ShouldDispatchCommandAndReturnOk()
    {
        // Arrange
        var cowId = Guid.NewGuid();
        var payload = JsonSerializer.Serialize(new
        {
            CowId = cowId,
            DiagnosisDate = DateTime.UtcNow.ToString("O"),
            IsPregnant = true,
            Notes = "Gestação confirmada via USG"
        });
        var request = CreateHttpRequestWithBody(payload);

        var expectedDto = new PregnancyDiagnosisDto(
            Guid.NewGuid(), cowId, "BR-100", DateTime.UtcNow, DiagnosisMethod.Ultrasound, true, 45, "OK");

        _senderMock.Send(Arg.Any<RegisterPregnancyDiagnosisCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(expectedDto));

        // Act
        var result = await OfflineSyncEndpoints.HandleSyncOperationAsync(
            "Breeding", "RegisterDiagnosis", false, request, _userClaims, _tenantContextMock, _senderMock, CancellationToken.None);

        // Assert
        var statusCodeResult = result as IStatusCodeHttpResult;
        statusCodeResult.Should().NotBeNull();
        statusCodeResult!.StatusCode.Should().Be(StatusCodes.Status200OK);

        await _senderMock.Received(1).Send(
            Arg.Is<RegisterPregnancyDiagnosisCommand>(c => c.CowId == cowId && c.IsPregnant && c.TenantId == _tenantId && !c.Force),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleSyncOperation_Breeding_RegisterDiagnosis_WhenConflictAndForceFalse_ShouldReturn409Conflict()
    {
        // Arrange
        var cowId = Guid.NewGuid();
        var payload = JsonSerializer.Serialize(new { CowId = cowId, IsPregnant = true });
        var request = CreateHttpRequestWithBody(payload);

        _senderMock.Send(Arg.Any<RegisterPregnancyDiagnosisCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<PregnancyDiagnosisDto>(Error.Conflict("Cow.Inactive", "Matriz inativa.")));

        // Act
        var result = await OfflineSyncEndpoints.HandleSyncOperationAsync(
            "Breeding", "RegisterDiagnosis", false, request, _userClaims, _tenantContextMock, _senderMock, CancellationToken.None);

        // Assert
        var statusCodeResult = result as IStatusCodeHttpResult;
        statusCodeResult.Should().NotBeNull();
        statusCodeResult!.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task HandleSyncOperation_Breeding_RecordInsemination_WhenConflictAndForceFalse_ShouldReturn409Conflict()
    {
        // Arrange
        var payload = JsonSerializer.Serialize(new { CowTag = "BR-PREG", SemenBatchCode = "ANGUS-01" });
        var request = CreateHttpRequestWithBody(payload);

        _senderMock.Send(Arg.Any<RecordInseminationCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<Guid>(Error.Conflict("Cow.AlreadyPregnant", "Matriz já está prenhe.")));

        // Act
        var result = await OfflineSyncEndpoints.HandleSyncOperationAsync(
            "Breeding", "RecordInsemination", false, request, _userClaims, _tenantContextMock, _senderMock, CancellationToken.None);

        // Assert
        var statusCodeResult = result as IStatusCodeHttpResult;
        statusCodeResult.Should().NotBeNull();
        statusCodeResult!.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task HandleSyncOperation_Breeding_RecordInsemination_WhenForceTrue_ShouldPassForceToCommand()
    {
        // Arrange
        var payload = JsonSerializer.Serialize(new { CowTag = "BR-PREG", SemenBatchCode = "ANGUS-01" });
        var request = CreateHttpRequestWithBody(payload);

        _senderMock.Send(Arg.Any<RecordInseminationCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(Guid.NewGuid()));

        // Act
        var result = await OfflineSyncEndpoints.HandleSyncOperationAsync(
            "Breeding", "RecordInsemination", true, request, _userClaims, _tenantContextMock, _senderMock, CancellationToken.None);

        // Assert
        var statusCodeResult = result as IStatusCodeHttpResult;
        statusCodeResult.Should().NotBeNull();
        statusCodeResult!.StatusCode.Should().Be(StatusCodes.Status200OK);

        await _senderMock.Received(1).Send(
            Arg.Is<RecordInseminationCommand>(c => c.CowTag == "BR-PREG" && c.Force),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleSyncOperation_Breeding_RegisterBreedingBatch_ShouldParseLinesAndDispatchCommand()
    {
        // Arrange
        var payload = JsonSerializer.Serialize(new
        {
            EventDate = DateTime.UtcNow.ToString("O"),
            Lines = new[]
            {
                new { TagId = "BR-01", BoarOrSemenRef = "NELORE-01" },
                new { TagId = "BR-02", BoarOrSemenRef = "NELORE-02" }
            }
        });
        var request = CreateHttpRequestWithBody(payload);

        _senderMock.Send(Arg.Any<RegisterBreedingBatchCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(2));

        // Act
        var result = await OfflineSyncEndpoints.HandleSyncOperationAsync(
            "Breeding", "RegisterBreedingBatch", true, request, _userClaims, _tenantContextMock, _senderMock, CancellationToken.None);

        // Assert
        var statusCodeResult = result as IStatusCodeHttpResult;
        statusCodeResult.Should().NotBeNull();
        statusCodeResult!.StatusCode.Should().Be(StatusCodes.Status200OK);

        await _senderMock.Received(1).Send(
            Arg.Is<RegisterBreedingBatchCommand>(c => c.Lines.Count == 2 && c.Force && c.TenantId == _tenantId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleSyncOperation_Growth_RecordWeighing_ShouldSetTenantIdWhenEmptyAndReturnOk()
    {
        // Arrange
        var payload = JsonSerializer.Serialize(new
        {
            TenantId = Guid.Empty,
            AnimalTagId = "BOI-99",
            WeighingDate = DateTime.UtcNow,
            WeightKg = 512.4m,
            Notes = "Curral Pesagem Balança Digital"
        });
        var request = CreateHttpRequestWithBody(payload);

        var weighingDto = new WeighingDto(
            Guid.NewGuid(),
            _tenantId,
            "BOI-99",
            null,
            DateTime.UtcNow,
            512.4m,
            0.54m,
            27.3m,
            0.95m,
            1.2m,
            false,
            "Curral",
            DateTime.UtcNow);

        _senderMock.Send(Arg.Any<RecordWeighingCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(weighingDto));

        // Act
        var result = await OfflineSyncEndpoints.HandleSyncOperationAsync(
            "Growth", "RecordWeighing", false, request, _userClaims, _tenantContextMock, _senderMock, CancellationToken.None);

        // Assert
        var statusCodeResult = result as IStatusCodeHttpResult;
        statusCodeResult.Should().NotBeNull();
        statusCodeResult!.StatusCode.Should().Be(StatusCodes.Status200OK);

        await _senderMock.Received(1).Send(
            Arg.Is<RecordWeighingCommand>(c => c.AnimalTagId == "BOI-99" && c.TenantId == _tenantId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleSyncOperation_Sanitary_ApplyVaccine_ShouldDispatchApplyTreatmentCommand()
    {
        // Arrange
        var payload = JsonSerializer.Serialize(new
        {
            ProductCommercialName = "Vacina Aftosa Bivalente",
            Type = TreatmentType.Vaccination,
            Dosage = "5ml",
            WithdrawalDays = 0,
            ApplicationDateUtc = DateTime.UtcNow
        });
        var request = CreateHttpRequestWithBody(payload);

        var treatmentDto = new TreatmentRecordDto(
            Guid.NewGuid(),
            null,
            null,
            "Vacina Aftosa Bivalente",
            TreatmentType.Vaccination,
            null,
            "5ml",
            0,
            DateTime.UtcNow,
            DateTime.UtcNow,
            false,
            "Dr. Carlos",
            null);

        _senderMock.Send(Arg.Any<ApplyTreatmentCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(treatmentDto));

        // Act
        var result = await OfflineSyncEndpoints.HandleSyncOperationAsync(
            "Sanitary", "ApplyVaccine", false, request, _userClaims, _tenantContextMock, _senderMock, CancellationToken.None);

        // Assert
        var statusCodeResult = result as IStatusCodeHttpResult;
        statusCodeResult.Should().NotBeNull();
        statusCodeResult!.StatusCode.Should().Be(StatusCodes.Status200OK);

        await _senderMock.Received(1).Send(
            Arg.Is<ApplyTreatmentCommand>(c => c.ProductCommercialName == "Vacina Aftosa Bivalente"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleSyncOperation_WhenPayloadIsDoublySerializedJsonString_ShouldUnescapeResiliently()
    {
        // Arrange - string escapada entre aspas
        var innerJson = JsonSerializer.Serialize(new { CowTag = "BR-UNESCAPE", SemenBatchCode = "NELORE-01" });
        var doubleEscapedPayload = JsonSerializer.Serialize(innerJson); // "\""{...}\"""
        var request = CreateHttpRequestWithBody(doubleEscapedPayload);

        _senderMock.Send(Arg.Any<RecordInseminationCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(Guid.NewGuid()));

        // Act
        var result = await OfflineSyncEndpoints.HandleSyncOperationAsync(
            "Breeding", "RecordInsemination", false, request, _userClaims, _tenantContextMock, _senderMock, CancellationToken.None);

        // Assert
        var statusCodeResult = result as IStatusCodeHttpResult;
        statusCodeResult.Should().NotBeNull();
        statusCodeResult!.StatusCode.Should().Be(StatusCodes.Status200OK);

        await _senderMock.Received(1).Send(
            Arg.Is<RecordInseminationCommand>(c => c.CowTag == "BR-UNESCAPE"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleSyncOperation_Breeding_RegisterCow_ShouldDispatchCommandAndReturnCreated()
    {
        // Arrange
        var payload = JsonSerializer.Serialize(new
        {
            EarTag = "NEL-901",
            Breed = "Nelore",
            Category = "Matriz",
            BodyConditionScore = 3.5m
        });
        var request = CreateHttpRequestWithBody(payload);

        var expectedDto = new CowDetailDto(
            Guid.NewGuid(), "NEL-901", null, null, null, null, null, "Nelore", "Nascimento Interno",
            null, DateTime.UtcNow, null, null, null, 3.5m, "Matriz",
            ReproductiveStatus.Open, 0, null, null, null, Array.Empty<TimelineEventDto>());

        _senderMock.Send(Arg.Any<CreateCowCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(expectedDto));

        // Act
        var result = await OfflineSyncEndpoints.HandleSyncOperationAsync(
            "Breeding", "RegisterCow", false, request, _userClaims, _tenantContextMock, _senderMock, CancellationToken.None);

        // Assert
        var statusCodeResult = result as IStatusCodeHttpResult;
        statusCodeResult.Should().NotBeNull();
        statusCodeResult!.StatusCode.Should().Be(StatusCodes.Status200OK);

        await _senderMock.Received(1).Send(
            Arg.Is<CreateCowCommand>(c => c.EarTag == "NEL-901" && c.TenantId == _tenantId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleSyncOperation_Calving_RecordCalving_ShouldDispatchCommandAndReturnOk()
    {
        // Arrange
        var motherId = Guid.NewGuid();
        var payload = JsonSerializer.Serialize(new
        {
            MotherCowId = motherId,
            CalvingDate = DateTime.UtcNow,
            Type = 1,
            CalfTagId = "BEZ-101",
            CalfSex = "M",
            CalfBreed = "Nelore",
            CalfBirthWeightKg = 34.5m,
            Condition = 1
        });
        var request = CreateHttpRequestWithBody(payload);

        var expectedDto = new CalvingDto(
            Guid.NewGuid(), motherId, DateTime.UtcNow,
            CriaCerto.Modules.Calving.Application.Domain.CalvingType.Normal,
            Guid.NewGuid(), "BEZ-101",
            CriaCerto.Modules.Calving.Application.Domain.BirthCondition.Live);

        _senderMock.Send(Arg.Any<RegisterCalvingCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(expectedDto));

        // Act
        var result = await OfflineSyncEndpoints.HandleSyncOperationAsync(
            "Calving", "RecordCalving", false, request, _userClaims, _tenantContextMock, _senderMock, CancellationToken.None);

        // Assert
        var statusCodeResult = result as IStatusCodeHttpResult;
        statusCodeResult.Should().NotBeNull();
        statusCodeResult!.StatusCode.Should().Be(StatusCodes.Status200OK);

        await _senderMock.Received(1).Send(
            Arg.Is<RegisterCalvingCommand>(c => c.CalfTagId == "BEZ-101" && c.TenantId == _tenantId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleSyncOperation_Nutrition_RecordFeedlotTmr_ShouldDispatchCommandAndReturnOk()
    {
        // Arrange
        var lotId = Guid.NewGuid();
        var rationId = Guid.NewGuid();
        var payload = JsonSerializer.Serialize(new
        {
            LotId = lotId,
            FeedRationId = rationId,
            FeedingTime = DateTime.UtcNow,
            OfferedAsFedKg = 1500m,
            TroughScore = 1,
            HeadCountAtFeeding = 80
        });
        var request = CreateHttpRequestWithBody(payload);

        var expectedDto = new DailyFeedBatchDto(
            Guid.NewGuid(), _tenantId, lotId, rationId, DateTime.UtcNow, 1500m, 750m, TroughScore.Score1_ThinLayer, 80);

        _senderMock.Send(Arg.Any<RecordFeedlotTmrCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(expectedDto));

        // Act
        var result = await OfflineSyncEndpoints.HandleSyncOperationAsync(
            "Nutrition", "RecordFeedlotTmr", false, request, _userClaims, _tenantContextMock, _senderMock, CancellationToken.None);

        // Assert
        var statusCodeResult = result as IStatusCodeHttpResult;
        statusCodeResult.Should().NotBeNull();
        statusCodeResult!.StatusCode.Should().Be(StatusCodes.Status200OK);

        await _senderMock.Received(1).Send(
            Arg.Is<RecordFeedlotTmrCommand>(c => c.LotId == lotId && c.OfferedAsFedKg == 1500m && c.TenantId == _tenantId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleSyncOperation_Nutrition_RecordSupplementation_ShouldDispatchCommandAndReturnOk()
    {
        // Arrange
        var paddockId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        var rationId = Guid.NewGuid();
        var payload = JsonSerializer.Serialize(new
        {
            PaddockId = paddockId,
            LotId = lotId,
            FeedRationId = rationId,
            DistributionDate = DateTime.UtcNow,
            QuantityKg = 120m,
            HeadCount = 60
        });
        var request = CreateHttpRequestWithBody(payload);

        var expectedDto = new PastureSupplementationDto(
            Guid.NewGuid(), _tenantId, paddockId, lotId, rationId, DateTime.UtcNow, 120m, 60, 200m);

        _senderMock.Send(Arg.Any<RecordSupplementationCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(expectedDto));

        // Act
        var result = await OfflineSyncEndpoints.HandleSyncOperationAsync(
            "Nutrition", "RecordSupplementation", false, request, _userClaims, _tenantContextMock, _senderMock, CancellationToken.None);

        // Assert
        var statusCodeResult = result as IStatusCodeHttpResult;
        statusCodeResult.Should().NotBeNull();
        statusCodeResult!.StatusCode.Should().Be(StatusCodes.Status200OK);

        await _senderMock.Received(1).Send(
            Arg.Is<RecordSupplementationCommand>(c => c.PaddockId == paddockId && c.QuantityKg == 120m && c.TenantId == _tenantId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleSyncOperation_Nutrition_RestockSilo_ShouldDispatchCommandAndReturnOk()
    {
        // Arrange
        var siloId = Guid.NewGuid();
        var payload = JsonSerializer.Serialize(new
        {
            Id = siloId,
            AddedKg = 6000m,
            NewUnitCostPerKg = 1.30m
        });
        var request = CreateHttpRequestWithBody(payload);

        var expectedDto = new SiloStockDto(
            siloId, _tenantId, "Milho Grão", FeedCategory.BulkGrain, 16000m, 1.30m, 88m, 2000m, DateTime.UtcNow, false);

        _senderMock.Send(Arg.Any<RestockSiloCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(expectedDto));

        // Act
        var result = await OfflineSyncEndpoints.HandleSyncOperationAsync(
            "Nutrition", "RestockSilo", false, request, _userClaims, _tenantContextMock, _senderMock, CancellationToken.None);

        // Assert
        var statusCodeResult = result as IStatusCodeHttpResult;
        statusCodeResult.Should().NotBeNull();
        statusCodeResult!.StatusCode.Should().Be(StatusCodes.Status200OK);

        await _senderMock.Received(1).Send(
            Arg.Is<RestockSiloCommand>(c => c.Id == siloId && c.AddedKg == 6000m && c.TenantId == _tenantId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleSyncOperation_WhenPayloadIsEmpty_ShouldReturn400BadRequestWithEmptyPayload()
    {
        // Arrange
        var request = CreateHttpRequestWithBody("   ");

        // Act
        var result = await OfflineSyncEndpoints.HandleSyncOperationAsync(
            "Breeding", "RegisterDiagnosis", false, request, _userClaims, _tenantContextMock, _senderMock, CancellationToken.None);

        // Assert
        var statusCodeResult = result as IStatusCodeHttpResult;
        statusCodeResult.Should().NotBeNull();
        statusCodeResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task HandleSyncOperation_WhenModuleOrActionUnsupported_ShouldReturn400BadRequestWithUnsupportedAction()
    {
        // Arrange
        var request = CreateHttpRequestWithBody("{\"some\":\"data\"}");

        // Act
        var result = await OfflineSyncEndpoints.HandleSyncOperationAsync(
            "UnsupportedModule", "UnknownAction", false, request, _userClaims, _tenantContextMock, _senderMock, CancellationToken.None);

        // Assert
        var statusCodeResult = result as IStatusCodeHttpResult;
        statusCodeResult.Should().NotBeNull();
        statusCodeResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }
}
