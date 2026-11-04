using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Features.Plans.Commands;
using CriaCerto.Modules.Backoffice.Application.Features.Plans.Dtos;
using CriaCerto.Modules.Backoffice.Application.Features.Plans.Queries;
using CriaCerto.Modules.Backoffice.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CriaCerto.Modules.Backoffice.UnitTests.Features;

public class ModularCatalogFeaturesTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BackofficeDbContext _db;

    public ModularCatalogFeaturesTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<BackofficeDbContext>().UseSqlite(_connection).Options;
        _db = new BackofficeDbContext(options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Close();
        _connection.Dispose();
    }

    [Fact]
    public async Task CreateHeadTierCatalogCommand_ShouldCreateTierAndAuditLog()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var handler = new CreateHeadTierCatalogCommandHandler(_db);
        var command = new CreateHeadTierCatalogCommand(
            "tier_500",
            "Até 500 cabeças",
            1,
            500,
            1,
            true,
            adminId,
            "admin@criacerto.com",
            "127.0.0.1");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TierCode.Should().Be("TIER_500");
        result.Value.DisplayName.Should().Be("Até 500 cabeças");
        result.Value.MinHeads.Should().Be(1);
        result.Value.MaxHeads.Should().Be(500);

        var auditLog = await _db.AuditLogs.SingleOrDefaultAsync(a => a.Action == "HeadTierCatalog.Created");
        auditLog.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateHeadTierCatalogCommand_WhenCodeAlreadyExists_ShouldReturnFailure()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var handler = new CreateHeadTierCatalogCommandHandler(_db);
        var command = new CreateHeadTierCatalogCommand(
            "TIER_1500",
            "De 501 a 1.500 cabeças",
            501,
            1500,
            2,
            true,
            adminId,
            "admin@criacerto.com",
            "127.0.0.1");

        await handler.Handle(command, CancellationToken.None);

        // Act
        var duplicateResult = await handler.Handle(command with { DisplayName = "Duplicado" }, CancellationToken.None);

        // Assert
        duplicateResult.IsFailure.Should().BeTrue();
        duplicateResult.Error.Code.Should().Be("HeadTierCatalog.CodeAlreadyExists");
    }

    [Fact]
    public async Task UpdateHeadTierCatalogCommand_ShouldUpdateTierAndLogAudit()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var createHandler = new CreateHeadTierCatalogCommandHandler(_db);
        var created = (await createHandler.Handle(new CreateHeadTierCatalogCommand(
            "TIER_3000",
            "De 1.501 a 3.000 cabeças",
            1501,
            3000,
            3,
            true,
            adminId,
            "admin@criacerto.com",
            "127.0.0.1"), CancellationToken.None)).Value;

        var updateHandler = new UpdateHeadTierCatalogCommandHandler(_db);

        // Act
        var updateResult = await updateHandler.Handle(new UpdateHeadTierCatalogCommand(
            created.Id,
            "De 1.501 a 3.500 cabeças",
            1501,
            3500,
            3,
            true,
            adminId,
            "admin@criacerto.com",
            "127.0.0.1"), CancellationToken.None);

        // Assert
        updateResult.IsSuccess.Should().BeTrue();
        updateResult.Value.MaxHeads.Should().Be(3500);
        updateResult.Value.DisplayName.Should().Be("De 1.501 a 3.500 cabeças");

        var auditLog = await _db.AuditLogs.SingleOrDefaultAsync(a => a.Action == "HeadTierCatalog.Updated");
        auditLog.Should().NotBeNull();
    }

    [Fact]
    public async Task ModularCatalogLifecycle_CreateModule_DraftPrice_UpdateDraft_PublishAndDeprecate_ShouldSucceed()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var tierHandler = new CreateHeadTierCatalogCommandHandler(_db);
        var tier500 = (await tierHandler.Handle(new CreateHeadTierCatalogCommand(
            "TIER_500", "Até 500 cabeças", 1, 500, 1, true, adminId, "admin@criacerto.com", "127.0.0.1"), CancellationToken.None)).Value;
        var tier1500 = (await tierHandler.Handle(new CreateHeadTierCatalogCommand(
            "TIER_1500", "De 501 a 1.500 cabeças", 501, 1500, 2, true, adminId, "admin@criacerto.com", "127.0.0.1"), CancellationToken.None)).Value;

        var createModuleHandler = new CreateModuleCatalogCommandHandler(_db);
        var moduleDto = (await createModuleHandler.Handle(new CreateModuleCatalogCommand(
            "Breeding",
            "Reprodução & IATF",
            "Gestão de protocolos IATF, inseminação e diagnóstico de gestação.",
            "bi-heart-pulse",
            false,
            2,
            true,
            new List<ModuleTierPriceInputDto>
            {
                new(tier500.Id, 49m, 39.20m, "price_br_500_m", "price_br_500_a"),
                new(tier1500.Id, 89m, 71.20m, "price_br_1500_m", "price_br_1500_a")
            },
            adminId,
            "admin@criacerto.com",
            "127.0.0.1"), CancellationToken.None)).Value;

        moduleDto.DraftPrices.Should().HaveCount(2);
        var v1Tier500DraftId = moduleDto.DraftPrices.Single(p => p.HeadTierCatalogId == tier500.Id).Id;

        _db.ChangeTracker.Clear();

        // Update Draft before publishing
        var updateDraftHandler = new UpdateModuleTierPriceDraftCommandHandler(_db);
        var updatedDraftResult = await updateDraftHandler.Handle(new UpdateModuleTierPriceDraftCommand(
            v1Tier500DraftId,
            52m,
            41.60m,
            "price_br_500_m_v1",
            "price_br_500_a_v1",
            adminId,
            "admin@criacerto.com",
            "127.0.0.1"), CancellationToken.None);

        updatedDraftResult.IsSuccess.Should().BeTrue();
        updatedDraftResult.Value.MonthlyPrice.Should().Be(52m);

        _db.ChangeTracker.Clear();

        // Publish v1 for TIER_500
        var publishHandler = new PublishModuleTierPriceCommandHandler(_db);
        var pubV1Result = await publishHandler.Handle(new PublishModuleTierPriceCommand(
            v1Tier500DraftId,
            "Lançamento v1",
            adminId,
            "admin@criacerto.com",
            "127.0.0.1"), CancellationToken.None);

        pubV1Result.IsSuccess.Should().BeTrue();
        pubV1Result.Value.Status.Should().Be(PlanVersionStatus.Published.ToString());

        _db.ChangeTracker.Clear();

        // Create v2 Draft for TIER_500 and Publish (should deprecate v1)
        var createDraftHandler = new CreateModuleTierPriceDraftCommandHandler(_db);
        var v2DraftResult = await createDraftHandler.Handle(new CreateModuleTierPriceDraftCommand(
            moduleDto.Id,
            tier500.Id,
            59m,
            47.20m,
            "price_br_500_m_v2",
            "price_br_500_a_v2",
            adminId,
            "admin@criacerto.com",
            "127.0.0.1"), CancellationToken.None);

        v2DraftResult.IsSuccess.Should().BeTrue();
        v2DraftResult.Value.VersionNumber.Should().Be(2);

        _db.ChangeTracker.Clear();

        var pubV2Result = await publishHandler.Handle(new PublishModuleTierPriceCommand(
            v2DraftResult.Value.Id,
            "Reajuste anual v2",
            adminId,
            "admin@criacerto.com",
            "127.0.0.1"), CancellationToken.None);

        pubV2Result.IsSuccess.Should().BeTrue();
        pubV2Result.Value.Status.Should().Be(PlanVersionStatus.Published.ToString());

        // Query catalog to verify ActivePrices and PriceHistory
        var queryHandler = new GetModularCatalogQueriesHandler(_db);
        var getModuleResult = await queryHandler.Handle(new GetModuleCatalogByIdQuery(moduleDto.Id), CancellationToken.None);

        getModuleResult.IsSuccess.Should().BeTrue();
        getModuleResult.Value.ActivePrices.Should().ContainSingle(p => p.HeadTierCatalogId == tier500.Id && p.VersionNumber == 2 && p.MonthlyPrice == 59m);
        getModuleResult.Value.PriceHistory.Should().Contain(p => p.Id == v1Tier500DraftId && p.Status == PlanVersionStatus.Deprecated.ToString());

        var auditActions = await _db.AuditLogs.Select(a => a.Action).ToListAsync();
        auditActions.Should().Contain(new[]
        {
            "ModuleCatalog.Created",
            "ModuleTierPrice.DraftUpdated",
            "ModuleTierPrice.DraftCreated",
            "ModuleTierPrice.Published"
        });
    }
}

