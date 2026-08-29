using CriaCerto.Modules.Breeding.Application.Domain;
using CriaCerto.Modules.Breeding.Application.Features.BreedingOps;
using CriaCerto.Modules.Breeding.Application.Features.Plantel;
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
    public async Task ListBullsQueryHandler_ShouldReturnBullsAndReprodutorCows()
    {
        using var dbContext = CreateInMemoryDbContext();

        // 1. Touro na tabela Bulls
        var bullResult = Bull.Create("TOURO-01", "Barão da Mata", "Nelore", DateTime.UtcNow.AddYears(-4), _tenantId, "REG-123");
        bullResult.IsSuccess.Should().BeTrue();
        dbContext.Bulls.Add(bullResult.Value);

        // 2. Touro cadastrado no plantel (Cows com Category = Reprodutor)
        var cowBullResult = Cow.Create("TOURO-02", "Angus", DateTime.UtcNow.AddYears(-3), _tenantId, nickname: "Touro Black", category: "Reprodutor");
        cowBullResult.IsSuccess.Should().BeTrue();
        dbContext.Cows.Add(cowBullResult.Value);

        // 3. Matriz (não deve ser retornada como touro)
        var cowResult = Cow.Create("VACA-01", "Nelore", DateTime.UtcNow.AddYears(-3), _tenantId, nickname: "Mimosa", category: "Matriz");
        cowResult.IsSuccess.Should().BeTrue();
        dbContext.Cows.Add(cowResult.Value);

        await dbContext.SaveChangesAsync();

        var handler = new ListBullsQueryHandler(dbContext);
        var queryResult = await handler.Handle(new ListBullsQuery(_tenantId), CancellationToken.None);

        queryResult.IsSuccess.Should().BeTrue();
        var bulls = queryResult.Value;
        bulls.Should().HaveCount(2);
        bulls.Select(b => b.EarTag).Should().Contain(new[] { "TOURO-01", "TOURO-02" });
        bulls.Select(b => b.EarTag).Should().NotContain("VACA-01");
    }

    [Fact]
    public async Task RegisterIatfProtocolCommandHandler_WithBullId_ShouldAssignBullNameAndPersist()
    {
        using var dbContext = CreateInMemoryDbContext();

        var cowResult = Cow.Create("VACA-10", "Nelore", DateTime.UtcNow.AddYears(-3), _tenantId);
        dbContext.Cows.Add(cowResult.Value);

        var bullResult = Bull.Create("TOURO-99", "Imperador", "Nelore PO", DateTime.UtcNow.AddYears(-5), _tenantId);
        dbContext.Bulls.Add(bullResult.Value);

        await dbContext.SaveChangesAsync();

        var handler = new RegisterIatfProtocolCommandHandler(dbContext);
        var command = new RegisterIatfProtocolCommand(
            "Lote IATF Primavera 2026",
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(10),
            Guid.NewGuid(),
            new List<Guid> { cowResult.Value.Id },
            _tenantId,
            BullId: bullResult.Value.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.BullId.Should().Be(bullResult.Value.Id);
        result.Value.BullName.Should().Contain("TOURO-99");
        result.Value.BullName.Should().Contain("Imperador");

        var persisted = await dbContext.IatfProtocols.FirstOrDefaultAsync(p => p.Id == result.Value.Id);
        persisted.Should().NotBeNull();
        persisted!.BullId.Should().Be(bullResult.Value.Id);
        persisted.BullName.Should().Be(result.Value.BullName);
    }

    [Fact]
    public async Task RegisterIatfProtocolCommandHandler_WithoutBullId_ShouldPersistExternalBull()
    {
        using var dbContext = CreateInMemoryDbContext();

        var cowResult = Cow.Create("VACA-20", "Nelore", DateTime.UtcNow.AddYears(-3), _tenantId);
        dbContext.Cows.Add(cowResult.Value);
        await dbContext.SaveChangesAsync();

        var handler = new RegisterIatfProtocolCommandHandler(dbContext);
        var command = new RegisterIatfProtocolCommand(
            "Lote IATF Semen Externo",
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(10),
            Guid.NewGuid(),
            new List<Guid> { cowResult.Value.Id },
            _tenantId,
            BullId: null);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.BullId.Should().BeNull();
        result.Value.BullName.Should().BeNull();

        var persisted = await dbContext.IatfProtocols.FirstOrDefaultAsync(p => p.Id == result.Value.Id);
        persisted.Should().NotBeNull();
        persisted!.BullId.Should().BeNull();
        persisted.BullName.Should().BeNull();
    }
}
