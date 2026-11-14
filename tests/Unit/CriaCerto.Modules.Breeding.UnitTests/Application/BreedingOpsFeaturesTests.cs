using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Breeding.Application.Domain;
using CriaCerto.Modules.Breeding.Application.Features.BreedingOps;
using CriaCerto.Modules.Breeding.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CriaCerto.Modules.Breeding.UnitTests.Application;

public class BreedingOpsFeaturesTests
{
    private readonly Guid _tenantId = Guid.NewGuid();

    private static BreedingDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<BreedingDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        var context = new BreedingDbContext(options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public async Task RegisterIatfProtocolCommandHandler_WithBullId_ShouldResolveBullNameAndSave()
    {
        using var dbContext = CreateInMemoryDbContext();

        var cow = Cow.Create("MATRIZ-01", "Nelore", DateTime.UtcNow.AddYears(-3), _tenantId, category: "Matriz").Value;
        var bull = Cow.Create("TOURO-01", "Nelore", DateTime.UtcNow.AddYears(-4), _tenantId, nickname: "Titan", category: "Reprodutor").Value;

        dbContext.Cows.AddRange(cow, bull);
        await dbContext.SaveChangesAsync();

        var handler = new RegisterIatfProtocolCommandHandler(dbContext);
        var command = new RegisterIatfProtocolCommand(
            "Lote IATF Primavera",
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(10),
            Guid.NewGuid(),
            new List<Guid> { cow.Id },
            _tenantId,
            bull.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.BullId.Should().Be(bull.Id);
        result.Value.BullName.Should().Contain("TOURO-01");
        result.Value.BullName.Should().Contain("Titan");

        var protocol = await dbContext.IatfProtocols.FirstOrDefaultAsync(p => p.Id == result.Value.Id);
        protocol.Should().NotBeNull();
        protocol!.BullId.Should().Be(bull.Id);
        protocol.BullName.Should().Be(result.Value.BullName);
    }

    [Fact]
    public async Task RegisterIatfProtocolCommandHandler_WithoutBullId_ShouldSaveWithNullBullInfo()
    {
        using var dbContext = CreateInMemoryDbContext();

        var cow = Cow.Create("MATRIZ-02", "Nelore", DateTime.UtcNow.AddYears(-3), _tenantId, category: "Matriz").Value;
        dbContext.Cows.Add(cow);
        await dbContext.SaveChangesAsync();

        var handler = new RegisterIatfProtocolCommandHandler(dbContext);
        var command = new RegisterIatfProtocolCommand(
            "Lote IATF Externo",
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(10),
            Guid.NewGuid(),
            new List<Guid> { cow.Id },
            _tenantId,
            BullId: null);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.BullId.Should().BeNull();
        result.Value.BullName.Should().BeNull();

        var protocol = await dbContext.IatfProtocols.FirstOrDefaultAsync(p => p.Id == result.Value.Id);
        protocol.Should().NotBeNull();
        protocol!.BullId.Should().BeNull();
        protocol.BullName.Should().BeNull();
    }

    [Fact]
    public async Task RegisterPregnancyDiagnosisCommandHandler_WhenCowNotFound_ShouldReturnNotFound()
    {
        using var dbContext = CreateInMemoryDbContext();
        var handler = new RegisterPregnancyDiagnosisCommandHandler(dbContext);

        var command = new RegisterPregnancyDiagnosisCommand(
            Guid.NewGuid(),
            DateTime.UtcNow,
            DiagnosisMethod.Ultrasound,
            true,
            _tenantId);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Cow.NotFound");
    }

    [Fact]
    public async Task RegisterPregnancyDiagnosisCommandHandler_WhenCowExists_ShouldSaveDiagnosisAndUpdateStatus()
    {
        using var dbContext = CreateInMemoryDbContext();
        var cow = Cow.Create("MATRIZ-DIAG-01", "Nelore", DateTime.UtcNow.AddYears(-3), _tenantId).Value;
        dbContext.Cows.Add(cow);
        await dbContext.SaveChangesAsync();

        var handler = new RegisterPregnancyDiagnosisCommandHandler(dbContext);
        var command = new RegisterPregnancyDiagnosisCommand(
            cow.Id,
            DateTime.UtcNow,
            DiagnosisMethod.Ultrasound,
            true,
            _tenantId,
            GestationalAgeDays: 45,
            Notes: "Gestação confirmada");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.CowId.Should().Be(cow.Id);
        result.Value.IsPregnant.Should().BeTrue();
        result.Value.GestationalAgeDays.Should().Be(45);

        cow.Status.Should().Be(ReproductiveStatus.Pregnant);
        var diagnosis = await dbContext.PregnancyDiagnoses.FirstOrDefaultAsync(d => d.CowId == cow.Id);
        diagnosis.Should().NotBeNull();
        diagnosis!.IsPregnant.Should().BeTrue();
    }

    [Fact]
    public async Task RegisterPregnancyDiagnosisCommandHandler_WhenCowInactiveAndForceFalse_ShouldReturnConflict()
    {
        using var dbContext = CreateInMemoryDbContext();
        var cow = Cow.Create("MATRIZ-INACTIVE-01", "Nelore", DateTime.UtcNow.AddYears(-3), _tenantId).Value;
        cow.MarkAsCulled();
        dbContext.Cows.Add(cow);
        await dbContext.SaveChangesAsync();

        var handler = new RegisterPregnancyDiagnosisCommandHandler(dbContext);
        var command = new RegisterPregnancyDiagnosisCommand(
            cow.Id,
            DateTime.UtcNow,
            DiagnosisMethod.Ultrasound,
            true,
            _tenantId,
            Force: false);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Cow.Inactive");
    }

    [Fact]
    public async Task RegisterPregnancyDiagnosisCommandHandler_WhenCowInactiveAndForceTrue_ShouldOverrideAndSucceed()
    {
        using var dbContext = CreateInMemoryDbContext();
        var cow = Cow.Create("MATRIZ-INACTIVE-02", "Nelore", DateTime.UtcNow.AddYears(-3), _tenantId).Value;
        cow.MarkAsCulled();
        dbContext.Cows.Add(cow);
        await dbContext.SaveChangesAsync();

        var handler = new RegisterPregnancyDiagnosisCommandHandler(dbContext);
        var command = new RegisterPregnancyDiagnosisCommand(
            cow.Id,
            DateTime.UtcNow,
            DiagnosisMethod.Ultrasound,
            true,
            _tenantId,
            Force: true);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        cow.Status.Should().Be(ReproductiveStatus.Pregnant);
    }

    [Fact]
    public async Task RecordInseminationCommandHandler_WhenCowAlreadyPregnantAndForceFalse_ShouldReturnConflict()
    {
        using var dbContext = CreateInMemoryDbContext();
        var cow = Cow.Create("MATRIZ-INSEM-01", "Nelore", DateTime.UtcNow.AddYears(-3), _tenantId).Value;
        cow.RecordPregnancyDiagnosis(true, DateTime.UtcNow.AddDays(-30));
        dbContext.Cows.Add(cow);
        await dbContext.SaveChangesAsync();

        var handler = new RecordInseminationCommandHandler(dbContext);
        var command = new RecordInseminationCommand(
            "MATRIZ-INSEM-01",
            DateTime.UtcNow,
            "ANGUS-01",
            _tenantId,
            Force: false);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Cow.AlreadyPregnant");
    }

    [Fact]
    public async Task RecordInseminationCommandHandler_WhenCowAlreadyPregnantAndForceTrue_ShouldOverrideAndSucceed()
    {
        using var dbContext = CreateInMemoryDbContext();
        var cow = Cow.Create("MATRIZ-INSEM-02", "Nelore", DateTime.UtcNow.AddYears(-3), _tenantId).Value;
        cow.RecordPregnancyDiagnosis(true, DateTime.UtcNow.AddDays(-30));
        dbContext.Cows.Add(cow);
        await dbContext.SaveChangesAsync();

        var handler = new RecordInseminationCommandHandler(dbContext);
        var command = new RecordInseminationCommand(
            "MATRIZ-INSEM-02",
            DateTime.UtcNow,
            "ANGUS-01",
            _tenantId,
            Force: true);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        cow.Status.Should().Be(ReproductiveStatus.Inseminated);
    }

    [Fact]
    public async Task RegisterBreedingBatchCommandHandler_WhenBatchHasMultipleLines_ShouldProcessAndAutoCreateMissingCows()
    {
        using var dbContext = CreateInMemoryDbContext();
        var existingCow = Cow.Create("MATRIZ-BATCH-01", "Nelore", DateTime.UtcNow.AddYears(-3), _tenantId).Value;
        dbContext.Cows.Add(existingCow);
        await dbContext.SaveChangesAsync();

        var handler = new RegisterBreedingBatchCommandHandler(dbContext);
        var lines = new List<BreedingBatchSyncLine>
        {
            new("MATRIZ-BATCH-01", "NELORE-PO-01", 3.5m),
            new("MATRIZ-BATCH-02", "NELORE-PO-02", 3.0m) // Não existia previamente
        };

        var command = new RegisterBreedingBatchCommand(
            DateTime.UtcNow,
            lines,
            _tenantId,
            Force: false);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(2);

        var createdCow = await dbContext.Cows.FirstOrDefaultAsync(c => c.EarTag == "MATRIZ-BATCH-02");
        createdCow.Should().NotBeNull();
        createdCow!.Status.Should().Be(ReproductiveStatus.Inseminated);
        existingCow.Status.Should().Be(ReproductiveStatus.Inseminated);
    }

    [Fact]
    public async Task RegisterBreedingBatchCommandHandler_WhenCowPregnantAndForceFalse_ShouldReturnConflict()
    {
        using var dbContext = CreateInMemoryDbContext();
        var cow = Cow.Create("MATRIZ-PREG-BATCH", "Nelore", DateTime.UtcNow.AddYears(-3), _tenantId).Value;
        cow.RecordPregnancyDiagnosis(true, DateTime.UtcNow.AddDays(-20));
        dbContext.Cows.Add(cow);
        await dbContext.SaveChangesAsync();

        var handler = new RegisterBreedingBatchCommandHandler(dbContext);
        var command = new RegisterBreedingBatchCommand(
            DateTime.UtcNow,
            new List<BreedingBatchSyncLine> { new("MATRIZ-PREG-BATCH", "SEMEN-01", null) },
            _tenantId,
            Force: false);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Cow.AlreadyPregnant");
    }

    [Fact]
    public async Task RegisterBreedingBatchCommandHandler_WhenCowPregnantAndForceTrue_ShouldOverrideAndSucceed()
    {
        using var dbContext = CreateInMemoryDbContext();
        var cow = Cow.Create("MATRIZ-PREG-FORCE", "Nelore", DateTime.UtcNow.AddYears(-3), _tenantId).Value;
        cow.RecordPregnancyDiagnosis(true, DateTime.UtcNow.AddDays(-20));
        dbContext.Cows.Add(cow);
        await dbContext.SaveChangesAsync();

        var handler = new RegisterBreedingBatchCommandHandler(dbContext);
        var command = new RegisterBreedingBatchCommand(
            DateTime.UtcNow,
            new List<BreedingBatchSyncLine> { new("MATRIZ-PREG-FORCE", "SEMEN-01", null) },
            _tenantId,
            Force: true);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(1);
        cow.Status.Should().Be(ReproductiveStatus.Inseminated);
    }
}
