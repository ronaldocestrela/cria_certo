using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Features.BackofficeTenants;
using CriaCerto.Modules.Tenancy.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Tenancy.UnitTests;

public class SuspendTenantForAdminCommandHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TenancyDbContext _dbContext;

    public SuspendTenantForAdminCommandHandlerTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<TenancyDbContext>().UseSqlite(_connection).Options;
        _dbContext = new TenancyDbContext(options);
        _dbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Close();
        _connection.Dispose();
    }

    [Fact]
    public async Task Handle_Should_SuspendTenant_WhenValid()
    {
        var tenantId = Guid.NewGuid();
        _dbContext.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Fazenda",
            CNPJ = "12.345.678/0001-90",
            CnpjNormalized = "12345678000190",
            Status = TenantLifecycle.ToStatusString(TenantStatus.Active),
            SubscribedPlan = "Starter",
            Capacity = 500,
            State = "MT",
            City = "Sinop",
            StateRegistration = "IE",
            Type = "Corte",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        var handler = new SuspendTenantForAdminCommandHandler(_dbContext);
        var result = await handler.Handle(
            new SuspendTenantForAdminCommand(tenantId, "Inadimplência confirmada pelo financeiro."),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Suspended");
        result.Value.StatusReason.Should().Be("Inadimplência confirmada pelo financeiro.");
    }

    [Fact]
    public async Task Handle_Should_Fail_WhenTenantIsProtected()
    {
        var tenantId = Guid.NewGuid();
        _dbContext.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Fazenda Protegida",
            CNPJ = "12.345.678/0001-90",
            CnpjNormalized = "12345678000190",
            Status = TenantLifecycle.ToStatusString(TenantStatus.Active),
            IsProtected = true,
            SubscribedPlan = "Starter",
            Capacity = 500,
            State = "MT",
            City = "Sinop",
            StateRegistration = "IE",
            Type = "Corte",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        var handler = new SuspendTenantForAdminCommandHandler(_dbContext);
        var result = await handler.Handle(
            new SuspendTenantForAdminCommand(tenantId, "Tentativa de suspensão em tenant protegido."),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.ProtectedTenant");
    }
}
