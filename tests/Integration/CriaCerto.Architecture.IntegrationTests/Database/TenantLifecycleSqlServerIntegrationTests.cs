using CriaCerto.BuildingBlocks.Infrastructure.Persistence;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Features.BackofficeTenants;
using CriaCerto.Modules.Tenancy.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace CriaCerto.Architecture.IntegrationTests.Database;

public sealed class TenantLifecycleSqlServerIntegrationTests : IAsyncLifetime
{
    private MsSqlContainer? _sqlContainer;
    private string _connectionString = string.Empty;
    private bool _isDockerAvailable;

    public async Task InitializeAsync()
    {
        (_sqlContainer, _connectionString, _isDockerAvailable) =
            await MigrationTestSupport.StartSqlServerContainerAsync();
    }

    public async Task DisposeAsync()
    {
        if (_sqlContainer is not null)
        {
            await _sqlContainer.DisposeAsync();
        }
    }

    [Fact]
    public async Task SuspendTenantForAdmin_Should_Persist_Status_And_Reason()
    {
        if (!_isDockerAvailable) return;
        var options = MigrationTestSupport.CreateSqlServerOptions<TenancyDbContext>(
            _connectionString,
            "tenancy");

        await using var db = new TenancyDbContext(options);
        DatabaseMigrationRunner.ApplyMigrations(db, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

        var tenantId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Farm Lifecycle",
            CNPJ = "12.345.678/0001-90",
            CnpjNormalized = "12345678000190",
            State = "MT",
            City = "Sinop",
            Status = TenantLifecycle.ToStatusString(TenantStatus.Active),
            SubscribedPlan = "Starter",
            Capacity = 500,
            StateRegistration = "IE",
            Type = "Corte",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        await db.SaveChangesAsync();

        var handler = new SuspendTenantForAdminCommandHandler(db);
        var result = await handler.Handle(
            new SuspendTenantForAdminCommand(tenantId, "Inadimplência confirmada pelo financeiro."),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Suspended");
        result.Value.StatusReason.Should().Be("Inadimplência confirmada pelo financeiro.");

        var persisted = await db.Tenants.AsNoTracking().FirstAsync(t => t.Id == tenantId);
        persisted.Status.Should().Be("Suspended");
        persisted.StatusReason.Should().Be("Inadimplência confirmada pelo financeiro.");
        persisted.StatusChangedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task SuspendTenantForAdmin_Should_Fail_When_Tenant_Is_Protected()
    {
        if (!_isDockerAvailable) return;
        var options = MigrationTestSupport.CreateSqlServerOptions<TenancyDbContext>(
            _connectionString,
            "tenancy");

        await using var db = new TenancyDbContext(options);
        DatabaseMigrationRunner.ApplyMigrations(db, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

        var tenantId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Protected Farm",
            CNPJ = "98.765.432/0001-10",
            CnpjNormalized = "98765432000110",
            State = "MT",
            City = "Sinop",
            Status = TenantLifecycle.ToStatusString(TenantStatus.Active),
            IsProtected = true,
            SubscribedPlan = "Starter",
            Capacity = 500,
            StateRegistration = "IE",
            Type = "Corte",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        await db.SaveChangesAsync();

        var handler = new SuspendTenantForAdminCommandHandler(db);
        var result = await handler.Handle(
            new SuspendTenantForAdminCommand(tenantId, "Tentativa de suspensão em tenant protegido."),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.ProtectedTenant");
    }
}
