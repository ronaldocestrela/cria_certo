using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Features.GetProductionUnits;
using CriaCerto.Modules.Tenancy.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Tenancy.UnitTests;

public class GetProductionUnitsQueryHandlerTests : IDisposable
{
    private readonly SqliteConnection _sqliteConnection;
    private readonly TenancyDbContext _dbContext;

    public GetProductionUnitsQueryHandlerTests()
    {
        _sqliteConnection = new SqliteConnection("Filename=:memory:");
        _sqliteConnection.Open();

        var options = new DbContextOptionsBuilder<TenancyDbContext>()
            .UseSqlite(_sqliteConnection)
            .Options;

        _dbContext = new TenancyDbContext(options);
        _dbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _sqliteConnection.Close();
        _sqliteConnection.Dispose();
    }

    [Fact]
    public async Task Handle_Should_Return_Only_Production_Units_For_Requested_Tenant()
    {
        // Arrange
        var tenant1 = Guid.NewGuid();
        var tenant2 = Guid.NewGuid();

        _dbContext.Tenants.AddRange(
            new Tenant { Id = tenant1, Name = "Tenant 1", CNPJ = "11.111.111/0001-11", CnpjNormalized = "11111111000111", State = "MT", City = "A", Status = "Active", SubscribedPlan = "Starter", Capacity = 100, StateRegistration = "IE", Type = "Corte", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow },
            new Tenant { Id = tenant2, Name = "Tenant 2", CNPJ = "22.222.222/0001-22", CnpjNormalized = "22222222000122", State = "MT", City = "B", Status = "Active", SubscribedPlan = "Starter", Capacity = 100, StateRegistration = "IE", Type = "Corte", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow }
        );

        _dbContext.ProductionUnits.AddRange(
            new ProductionUnit { Id = Guid.NewGuid(), TenantId = tenant1, Code = "UN-001-SFE", Name = "Unidade A", Type = "Retiro", Capacity = 1000, CurrentHeadCount = 500 },
            new ProductionUnit { Id = Guid.NewGuid(), TenantId = tenant1, Code = "UN-002-SFE", Name = "Unidade B", Type = "Creche", Capacity = 2000, CurrentHeadCount = 1000 },
            new ProductionUnit { Id = Guid.NewGuid(), TenantId = tenant2, Code = "UN-001-SF2", Name = "Unidade Outro Tenant", Type = "Engorda", Capacity = 500, CurrentHeadCount = 100 }
        );
        await _dbContext.SaveChangesAsync();

        var handler = new GetProductionUnitsQueryHandler(_dbContext);
        var query = new GetProductionUnitsQuery(tenant1);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Select(u => u.Name).Should().Contain(new[] { "Unidade A", "Unidade B" });
        result.Value.First(u => u.Name == "Unidade A").OccupancyPercentage.Should().Be(50.0m);
    }
}
