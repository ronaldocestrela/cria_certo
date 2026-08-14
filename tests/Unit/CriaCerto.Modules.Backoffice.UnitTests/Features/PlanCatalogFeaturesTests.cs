using CriaCerto.BuildingBlocks.Abstractions.Results;
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

public class PlanCatalogFeaturesTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BackofficeDbContext _db;

    public PlanCatalogFeaturesTests()
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
    public async Task CreatePlanCatalogCommand_ShouldCreateCatalogAndAuditLog()
    {
        // Arrange
        var db = _db;
        var adminId = Guid.NewGuid();
        var handler = new CreatePlanCatalogCommandHandler(db);

        var command = new CreatePlanCatalogCommand(
            "starter-test",
            "Plano Starter Teste",
            "Descrição do plano starter",
            "PeDistributed",
            149.90m,
            119.90m,
            500,
            new List<PlanFeatureInputDto> { new("Modules.Breeding", "Reprodução", true) },
            new List<PlanLimitInputDto> { new("MaxCattleHeads", 500, "cabeças") },
            adminId,
            "admin@criacerto.com",
            "127.0.0.1"
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Code.Should().Be("starter-test");
        result.Value.Name.Should().Be("Plano Starter Teste");
        result.Value.DraftVersion.Should().NotBeNull();
        result.Value.DraftVersion!.MonthlyPrice.Should().Be(149.90m);

        var auditLogs = await db.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle(a => a.Action == "PlanCatalog.Created");
    }

    [Fact]
    public async Task CreatePlanCatalogCommand_WhenCodeExists_ShouldReturnFailure()
    {
        // Arrange
        var db = _db;
        var adminId = Guid.NewGuid();
        var handler = new CreatePlanCatalogCommandHandler(db);

        var command1 = new CreatePlanCatalogCommand(
            "dupe-code",
            "Plano 1",
            "Desc 1",
            "PeDistributed",
            100m,
            90m,
            500,
            null,
            null,
            adminId,
            "admin@criacerto.com",
            "127.0.0.1"
        );

        await handler.Handle(command1, CancellationToken.None);

        var command2 = command1 with { Name = "Plano 2 Duplicado" };

        // Act
        var result = await handler.Handle(command2, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PlanCatalog.CodeAlreadyExists");
    }

    [Fact]
    public async Task PublishPlanVersionCommand_ShouldPublishVersionAndDeprecateOldOne()
    {
        // Arrange
        var db = _db;
        var adminId = Guid.NewGuid();
        var createHandler = new CreatePlanCatalogCommandHandler(db);

        var createCommand = new CreatePlanCatalogCommand(
            "pro-plan",
            "Plano Pro",
            "Desc Pro",
            "PeDistributed",
            299.90m,
            249.90m,
            2500,
            null,
            null,
            adminId,
            "admin@criacerto.com",
            "127.0.0.1"
        );

        var planDto = (await createHandler.Handle(createCommand, CancellationToken.None)).Value;
        var draftV1Id = planDto.DraftVersion!.Id;

        _db.ChangeTracker.Clear();

        var publishHandler = new PublishPlanVersionCommandHandler(db);

        // Act 1 - Publish v1
        var pubResult1 = await publishHandler.Handle(new PublishPlanVersionCommand(draftV1Id, "Primeiro lançamento", adminId, "admin@criacerto.com", "127.0.0.1"), CancellationToken.None);

        // Assert 1
        pubResult1.IsSuccess.Should().BeTrue();
        pubResult1.Value.Status.Should().Be(PlanVersionStatus.Published.ToString());

        _db.ChangeTracker.Clear();

        // Create v2 Draft
        var versionHandler = new CreatePlanVersionCommandHandler(db);
        var v2Dto = (await versionHandler.Handle(new CreatePlanVersionCommand(
            planDto.Id,
            "v2.0 - Reajuste",
            349.90m,
            299.90m,
            3000,
            null,
            null,
            null,
            null,
            adminId,
            "admin@criacerto.com",
            "127.0.0.1"
        ), CancellationToken.None)).Value;

        _db.ChangeTracker.Clear();

        // Act 2 - Publish v2
        var pubResult2 = await publishHandler.Handle(new PublishPlanVersionCommand(v2Dto.Id, "Reajuste anual", adminId, "admin@criacerto.com", "127.0.0.1"), CancellationToken.None);

        // Assert 2
        pubResult2.IsSuccess.Should().BeTrue();
        pubResult2.Value.Status.Should().Be(PlanVersionStatus.Published.ToString());

        var v1Entity = await db.PlanVersions.FirstAsync(v => v.Id == draftV1Id);
        v1Entity.Status.Should().Be(PlanVersionStatus.Deprecated);

        var auditLogs = await db.AuditLogs.Where(a => a.Action == "PlanVersion.Published").ToListAsync();
        auditLogs.Should().HaveCount(2);
    }

    [Fact]
    public async Task ComparePlanVersionsQuery_ShouldReturnDifferencesBetweenVersions()
    {
        // Arrange
        var db = _db;
        var adminId = Guid.NewGuid();
        var createPlanHandler = new CreatePlanCatalogCommandHandler(db);

        var planDto = (await createPlanHandler.Handle(new CreatePlanCatalogCommand(
            "starter-comp",
            "Starter Comp",
            "Desc",
            "PeDistributed",
            100m,
            80m,
            500,
            new List<PlanFeatureInputDto> { new("Modules.Breeding", "Reprodução", true) },
            new List<PlanLimitInputDto> { new("MaxCattleHeads", 500, "cabeças") },
            adminId,
            "admin@criacerto.com",
            "127.0.0.1"
        ), CancellationToken.None)).Value;

        var v1Id = planDto.DraftVersion!.Id;
        _db.ChangeTracker.Clear();

        var pubHandler = new PublishPlanVersionCommandHandler(db);
        await pubHandler.Handle(new PublishPlanVersionCommand(v1Id, "Pub v1", adminId, "admin@criacerto.com", "127.0.0.1"), CancellationToken.None);
        _db.ChangeTracker.Clear();

        var createVerHandler = new CreatePlanVersionCommandHandler(db);
        var v2Dto = (await createVerHandler.Handle(new CreatePlanVersionCommand(
            planDto.Id,
            "v2.0 Novo Módulo",
            150m,
            120m,
            1000,
            null,
            null,
            new List<PlanFeatureInputDto>
            {
                new("Modules.Breeding", "Reprodução", true),
                new("Modules.Sanitary", "Sanidade", true)
            },
            new List<PlanLimitInputDto> { new("MaxCattleHeads", 1000, "cabeças") },
            adminId,
            "admin@criacerto.com",
            "127.0.0.1"
        ), CancellationToken.None)).Value;
        _db.ChangeTracker.Clear();

        var queryHandler = new GetPlanCatalogQueriesHandler(db);

        // Act
        var compResult = await queryHandler.Handle(new ComparePlanVersionsQuery(v1Id, v2Dto.Id), CancellationToken.None);

        // Assert
        compResult.IsSuccess.Should().BeTrue();
        compResult.Value.AddedFeatures.Should().ContainSingle(f => f == "Modules.Sanitary");
        compResult.Value.PriceDifferenceMonthly.Should().Be(50m);
        compResult.Value.PriceDifferenceAnnual.Should().Be(40m);
    }

    [Fact]
    public async Task UpdateDraftPlanVersionCommand_ShouldUpdateDraftVersionSuccessfullyAndLogAudit()
    {
        // Arrange
        var db = _db;
        var adminId = Guid.NewGuid();
        var createPlanHandler = new CreatePlanCatalogCommandHandler(db);

        var planDto = (await createPlanHandler.Handle(new CreatePlanCatalogCommand(
            "update-test-plan",
            "Plano para Teste de Edição",
            "Descrição",
            "PeDistributed",
            199.90m,
            169.90m,
            1000,
            new List<PlanFeatureInputDto> { new("Modules.Breeding", "Reprodução", true) },
            new List<PlanLimitInputDto> { new("MaxCattleHeads", 1000, "cabeças") },
            adminId,
            "admin@criacerto.com",
            "127.0.0.1"
        ), CancellationToken.None)).Value;

        var draftId = planDto.DraftVersion!.Id;
        _db.ChangeTracker.Clear();

        var updateHandler = new UpdateDraftPlanVersionCommandHandler(db);
        var updateCommand = new UpdateDraftPlanVersionCommand(
            draftId,
            "v1.0 - Atualizado",
            249.90m,
            209.90m,
            1500,
            5,
            2,
            new List<PlanFeatureInputDto>
            {
                new("Modules.Breeding", "Reprodução", true),
                new("Modules.Calving", "Partos", true),
                new("Modules.Growth", "Manejo", true)
            },
            new List<PlanLimitInputDto>
            {
                new("MaxCattleHeads", 1500, "cabeças")
            },
            adminId,
            "admin@criacerto.com",
            "127.0.0.1"
        );

        // Act
        var result = await updateHandler.Handle(updateCommand, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.VersionName.Should().Be("v1.0 - Atualizado");
        result.Value.MonthlyPrice.Should().Be(249.90m);
        result.Value.HeadCapacityLimit.Should().Be(1500);
        result.Value.Features.Should().HaveCount(3);

        var auditLog = await db.AuditLogs.FirstOrDefaultAsync(a => a.Action == "PlanVersion.UpdatedDraft");
        auditLog.Should().NotBeNull();
        auditLog!.Resource.Should().Be($"PlanVersion/{draftId}");
    }
}
