using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Domain.Errors;
using CriaCerto.Modules.Tenancy.Infrastructure.Persistence;
using CriaCerto.Modules.Tenancy.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Tenancy.UnitTests;

public class TenantAccessGuardTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TenancyDbContext _dbContext;
    private readonly TenantAccessGuard _guard;

    public TenantAccessGuardTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<TenancyDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new TenancyDbContext(options);
        _dbContext.Database.EnsureCreated();

        _guard = new TenantAccessGuard(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Close();
        _connection.Dispose();
    }

    [Fact]
    public async Task EnsureProducerAccessAsync_Should_ReturnTenantNotFound_WhenTenantDoesNotExist()
    {
        // Act
        var result = await _guard.EnsureProducerAccessAsync(Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(TenancyErrors.TenantNotFound.Code);
    }

    [Fact]
    public async Task EnsureProducerAccessAsync_Should_ReturnSuccess_WhenTenantIsActive()
    {
        // Arrange
        var tenant = CreateTenant(TenantStatus.Active);
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _guard.EnsureProducerAccessAsync(tenant.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task EnsureProducerAccessAsync_Should_ReturnSuccess_WhenTenantIsTrialAndNotExpired()
    {
        // Arrange
        var tenant = CreateTenant(TenantStatus.Trial, currentPeriodEndUtc: DateTime.UtcNow.AddDays(7));
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _guard.EnsureProducerAccessAsync(tenant.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task EnsureProducerAccessAsync_Should_ReturnTrialExpired_WhenTenantIsTrialAndPeriodEndInPast()
    {
        // Arrange
        var tenant = CreateTenant(TenantStatus.Trial, currentPeriodEndUtc: DateTime.UtcNow.AddMinutes(-5));
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _guard.EnsureProducerAccessAsync(tenant.Id);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(TenancyErrors.TrialExpired.Code);
        result.Error.Type.Should().Be(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task EnsureProducerAccessAsync_Should_ReturnTenantNotAccessible_WhenTenantIsSuspended()
    {
        // Arrange
        var tenant = CreateTenant(TenantStatus.Suspended);
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _guard.EnsureProducerAccessAsync(tenant.Id);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(TenancyErrors.TenantNotAccessible.Code);
    }

    [Fact]
    public async Task EnsureProducerAccessAsync_Should_ReturnTenantNotAccessible_WhenTenantIsCancelled()
    {
        // Arrange
        var tenant = CreateTenant(TenantStatus.Cancelled);
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _guard.EnsureProducerAccessAsync(tenant.Id);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(TenancyErrors.TenantNotAccessible.Code);
    }

    private static Tenant CreateTenant(TenantStatus status, DateTime? currentPeriodEndUtc = null)
    {
        return new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Teste Acesso",
            CNPJ = "12.345.678/0001-99",
            CnpjNormalized = "12345678000199",
            Status = TenantLifecycle.ToStatusString(status),
            SubscribedPlan = "Starter",
            Capacity = 500,
            State = "MT",
            City = "Cuiabá",
            StateRegistration = "123456",
            Type = "Corte",
            CurrentPeriodEndUtc = currentPeriodEndUtc,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }
}
