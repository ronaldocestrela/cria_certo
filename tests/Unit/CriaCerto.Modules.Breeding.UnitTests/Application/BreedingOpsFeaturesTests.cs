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
}
