using System.Net;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Web.Client.Models;
using CriaCerto.Web.Client.Services;
using FluentAssertions;
using Microsoft.JSInterop;
using NSubstitute;
using Xunit;

namespace CriaCerto.Web.Client.UnitTests.Services;

public class BreedingOpsApiClientTests
{
    private readonly IJSRuntime _jsRuntimeMock;
    private readonly IOfflineSyncService _offlineSyncServiceMock;
    private readonly HttpMessageHandlerStub _httpHandlerStub;
    private readonly HttpClient _httpClient;
    private readonly BreedingOpsApiClient _sut;

    public BreedingOpsApiClientTests()
    {
        _jsRuntimeMock = Substitute.For<IJSRuntime>();
        _offlineSyncServiceMock = Substitute.For<IOfflineSyncService>();
        _httpHandlerStub = new HttpMessageHandlerStub();
        _httpClient = new HttpClient(_httpHandlerStub)
        {
            BaseAddress = new Uri("https://localhost:7000/")
        };

        _sut = new BreedingOpsApiClient(_httpClient, _jsRuntimeMock, _offlineSyncServiceMock);
    }

    [Fact]
    public async Task IsOnlineAsync_ShouldInitializeAndReturnOfflineSyncServiceStatus()
    {
        // Arrange
        _offlineSyncServiceMock.IsOnline.Returns(false);

        // Act
        var isOnline = await _sut.IsOnlineAsync();

        // Assert
        isOnline.Should().BeFalse();
        await _offlineSyncServiceMock.Received(1).InitializeAsync();
    }

    [Fact]
    public async Task GetPendingOpsCountAsync_ShouldInitializeAndReturnOfflineSyncServicePendingCount()
    {
        // Arrange
        _offlineSyncServiceMock.PendingCount.Returns(4);

        // Act
        var count = await _sut.GetPendingOpsCountAsync();

        // Assert
        count.Should().Be(4);
        await _offlineSyncServiceMock.Received(1).InitializeAsync();
    }

    [Fact]
    public async Task EnqueueDiagnosisAsync_ShouldDelegateToOfflineSyncService()
    {
        // Arrange
        var request = new RegisterPregnancyDiagnosisRequest(
            Guid.NewGuid(),
            new DateOnly(2026, 11, 14),
            PregnancyDiagnosisMethod.Ultrasound,
            PregnancyDiagnosisResult.Pregnant,
            "Confirmada em campo");

        _offlineSyncServiceMock
            .EnqueueOperationAsync("Breeding", "RegisterDiagnosis", request)
            .Returns(Result.Success());

        // Act
        var enqueued = await _sut.EnqueueDiagnosisAsync(request);

        // Assert
        enqueued.Should().BeTrue();
        await _offlineSyncServiceMock.Received(1).InitializeAsync();
        await _offlineSyncServiceMock.Received(1).EnqueueOperationAsync("Breeding", "RegisterDiagnosis", request);
    }

    [Fact]
    public async Task EnqueueBreedingBatchAsync_ShouldDelegateToOfflineSyncService()
    {
        // Arrange
        var request = new RegisterBreedingBatchRequest(
            new DateOnly(2026, 11, 14),
            new List<BreedingBatchLineDto>
            {
                new("BR-101", BreedingMethod.TimedArtificialInsemination, "NELORE-01", "Ricardo S.", BodyConditionScore.Ideal, "Curral 1", null)
            });

        _offlineSyncServiceMock
            .EnqueueOperationAsync("Breeding", "RegisterBreedingBatch", request)
            .Returns(Result.Success());

        // Act
        var enqueued = await _sut.EnqueueBreedingBatchAsync(request);

        // Assert
        enqueued.Should().BeTrue();
        await _offlineSyncServiceMock.Received(1).InitializeAsync();
        await _offlineSyncServiceMock.Received(1).EnqueueOperationAsync("Breeding", "RegisterBreedingBatch", request);
    }

    [Fact]
    public async Task SyncPendingOpsAsync_WhenOnlineAndHasPendingItems_ShouldForceSyncAndReturnDelta()
    {
        // Arrange
        var currentPending = 3;
        _offlineSyncServiceMock.IsOnline.Returns(true);
        _offlineSyncServiceMock.PendingCount.Returns(_ => currentPending);
        _offlineSyncServiceMock.ForceSyncAsync().Returns(_ =>
        {
            currentPending = 1;
            return Result.Success();
        });

        // Act
        var syncedCount = await _sut.SyncPendingOpsAsync();

        // Assert
        syncedCount.Should().Be(2);
        await _offlineSyncServiceMock.Received(1).ForceSyncAsync();
    }

    [Fact]
    public async Task RegisterDiagnosisAsync_WhenHttpRequestExceptionOccurs_ShouldSetOfflineAndEnqueue()
    {
        // Arrange
        _httpHandlerStub.ThrowNetworkException = true;
        var request = new RegisterPregnancyDiagnosisRequest(
            Guid.NewGuid(),
            new DateOnly(2026, 11, 14),
            PregnancyDiagnosisMethod.Ultrasound,
            PregnancyDiagnosisResult.Pregnant,
            null);

        _offlineSyncServiceMock
            .EnqueueOperationAsync("Breeding", "RegisterDiagnosis", request)
            .Returns(Result.Success());

        // Act
        var response = await _sut.RegisterDiagnosisAsync(request);

        // Assert
        response.Should().BeTrue();
        _offlineSyncServiceMock.Received(1).SetNetworkStatus(false);
        await _offlineSyncServiceMock.Received(1).EnqueueOperationAsync("Breeding", "RegisterDiagnosis", request);
    }

    [Fact]
    public async Task EnqueueDiagnosisAsync_WhenOfflineSyncFails_ShouldReturnFalse()
    {
        // Arrange
        var request = new RegisterPregnancyDiagnosisRequest(
            Guid.NewGuid(),
            new DateOnly(2026, 11, 14),
            PregnancyDiagnosisMethod.Ultrasound,
            PregnancyDiagnosisResult.Pregnant,
            null);

        _offlineSyncServiceMock
            .EnqueueOperationAsync("Breeding", "RegisterDiagnosis", request)
            .Returns(Result.Failure(Error.Failure("Sync.QueueError", "Falha no IndexedDB")));

        // Act
        var enqueued = await _sut.EnqueueDiagnosisAsync(request);

        // Assert
        enqueued.Should().BeFalse();
        await _offlineSyncServiceMock.Received(1).InitializeAsync();
    }

    [Fact]
    public async Task EnqueueBreedingBatchAsync_WhenOfflineSyncFails_ShouldReturnFalse()
    {
        // Arrange
        var request = new RegisterBreedingBatchRequest(
            new DateOnly(2026, 11, 14),
            new List<BreedingBatchLineDto>
            {
                new("BR-101", BreedingMethod.TimedArtificialInsemination, "NELORE-01", "Ricardo S.", BodyConditionScore.Ideal, "Curral 1", null)
            });

        _offlineSyncServiceMock
            .EnqueueOperationAsync("Breeding", "RegisterBreedingBatch", request)
            .Returns(Result.Failure(Error.Failure("Sync.QueueError", "Falha no IndexedDB")));

        // Act
        var enqueued = await _sut.EnqueueBreedingBatchAsync(request);

        // Assert
        enqueued.Should().BeFalse();
        await _offlineSyncServiceMock.Received(1).InitializeAsync();
    }

    [Theory]
    [InlineData(false, 3)]
    [InlineData(true, 0)]
    public async Task SyncPendingOpsAsync_WhenOfflineOrNoPendingItems_ShouldReturnZeroAndNotForceSync(bool isOnline, int pendingCount)
    {
        // Arrange
        _offlineSyncServiceMock.IsOnline.Returns(isOnline);
        _offlineSyncServiceMock.PendingCount.Returns(pendingCount);

        // Act
        var syncedCount = await _sut.SyncPendingOpsAsync();

        // Assert
        syncedCount.Should().Be(0);
        await _offlineSyncServiceMock.Received(1).InitializeAsync();
        await _offlineSyncServiceMock.DidNotReceive().ForceSyncAsync();
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, true)]
    [InlineData(HttpStatusCode.BadRequest, false)]
    public async Task RegisterDiagnosisAsync_WhenOnline_ShouldReturnHttpSuccessStatus(HttpStatusCode statusCode, bool expectedResult)
    {
        // Arrange
        _httpHandlerStub.StatusCode = statusCode;
        var request = new RegisterPregnancyDiagnosisRequest(
            Guid.NewGuid(),
            new DateOnly(2026, 11, 14),
            PregnancyDiagnosisMethod.Ultrasound,
            PregnancyDiagnosisResult.Pregnant,
            null);

        // Act
        var response = await _sut.RegisterDiagnosisAsync(request);

        // Assert
        response.Should().Be(expectedResult);
        _offlineSyncServiceMock.DidNotReceiveWithAnyArgs().SetNetworkStatus(default);
    }

    [Fact]
    public async Task RegisterDiagnosisAsync_WhenHttpRequestExceptionAndEnqueueFails_ShouldReturnNull()
    {
        // Arrange
        _httpHandlerStub.ThrowNetworkException = true;
        var request = new RegisterPregnancyDiagnosisRequest(
            Guid.NewGuid(),
            new DateOnly(2026, 11, 14),
            PregnancyDiagnosisMethod.Ultrasound,
            PregnancyDiagnosisResult.Pregnant,
            null);

        _offlineSyncServiceMock
            .EnqueueOperationAsync("Breeding", "RegisterDiagnosis", request)
            .Returns(Result.Failure(Error.Failure("Sync.QueueError", "Falha ao enfileirar")));

        // Act
        var response = await _sut.RegisterDiagnosisAsync(request);

        // Assert
        response.Should().BeNull();
        _offlineSyncServiceMock.Received(1).SetNetworkStatus(false);
    }

    [Fact]
    public async Task RegisterBreedingBatchAsync_WhenOnlineAndHttpSuccess_ShouldReturnResponseWithLineCount()
    {
        // Arrange
        _httpHandlerStub.StatusCode = HttpStatusCode.OK;
        var request = new RegisterBreedingBatchRequest(
            new DateOnly(2026, 11, 14),
            new List<BreedingBatchLineDto>
            {
                new("BR-101", BreedingMethod.TimedArtificialInsemination, "NELORE-01", "Ricardo S.", BodyConditionScore.Ideal, null, null),
                new("BR-102", BreedingMethod.TimedArtificialInsemination, "TOURO-02", "Ricardo S.", BodyConditionScore.Ideal, null, null)
            });

        // Act
        var response = await _sut.RegisterBreedingBatchAsync(request);

        // Assert
        response.Should().NotBeNull();
        response!.RegisteredCount.Should().Be(2);
        _offlineSyncServiceMock.DidNotReceiveWithAnyArgs().SetNetworkStatus(default);
    }

    [Fact]
    public async Task RegisterBreedingBatchAsync_WhenOnlineAndHttpFailure_ShouldReturnNull()
    {
        // Arrange
        _httpHandlerStub.StatusCode = HttpStatusCode.BadRequest;
        var request = new RegisterBreedingBatchRequest(
            new DateOnly(2026, 11, 14),
            new List<BreedingBatchLineDto>
            {
                new("BR-101", BreedingMethod.TimedArtificialInsemination, "NELORE-01", "Ricardo S.", BodyConditionScore.Ideal, null, null)
            });

        // Act
        var response = await _sut.RegisterBreedingBatchAsync(request);

        // Assert
        response.Should().BeNull();
    }

    [Fact]
    public async Task RegisterBreedingBatchAsync_WhenHttpRequestExceptionOccurs_ShouldSetOfflineAndEnqueue()
    {
        // Arrange
        _httpHandlerStub.ThrowNetworkException = true;
        var request = new RegisterBreedingBatchRequest(
            new DateOnly(2026, 11, 14),
            new List<BreedingBatchLineDto>
            {
                new("BR-101", BreedingMethod.TimedArtificialInsemination, "NELORE-01", "Ricardo S.", BodyConditionScore.Ideal, null, null)
            });

        _offlineSyncServiceMock
            .EnqueueOperationAsync("Breeding", "RegisterBreedingBatch", request)
            .Returns(Result.Success());

        // Act
        var response = await _sut.RegisterBreedingBatchAsync(request);

        // Assert
        response.Should().NotBeNull();
        response!.RegisteredCount.Should().Be(1);
        _offlineSyncServiceMock.Received(1).SetNetworkStatus(false);
        await _offlineSyncServiceMock.Received(1).EnqueueOperationAsync("Breeding", "RegisterBreedingBatch", request);
    }

    [Fact]
    public async Task RegisterBreedingBatchAsync_WhenHttpRequestExceptionAndEnqueueFails_ShouldReturnNull()
    {
        // Arrange
        _httpHandlerStub.ThrowNetworkException = true;
        var request = new RegisterBreedingBatchRequest(
            new DateOnly(2026, 11, 14),
            new List<BreedingBatchLineDto>
            {
                new("BR-101", BreedingMethod.TimedArtificialInsemination, "NELORE-01", "Ricardo S.", BodyConditionScore.Ideal, null, null)
            });

        _offlineSyncServiceMock
            .EnqueueOperationAsync("Breeding", "RegisterBreedingBatch", request)
            .Returns(Result.Failure(Error.Failure("Sync.QueueError", "Falha ao enfileirar lote")));

        // Act
        var response = await _sut.RegisterBreedingBatchAsync(request);

        // Assert
        response.Should().BeNull();
        _offlineSyncServiceMock.Received(1).SetNetworkStatus(false);
        await _offlineSyncServiceMock.Received(1).EnqueueOperationAsync("Breeding", "RegisterBreedingBatch", request);
    }

    private sealed class HttpMessageHandlerStub : HttpMessageHandler
    {
        public bool ThrowNetworkException { get; set; }
        public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (ThrowNetworkException)
            {
                throw new HttpRequestException("Simulated network drop in pasture/curral.");
            }

            return Task.FromResult(new HttpResponseMessage(StatusCode));
        }
    }
}

