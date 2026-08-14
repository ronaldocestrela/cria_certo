using CriaCerto.BuildingBlocks.Infrastructure.Persistence;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace CriaCerto.Architecture.IntegrationTests.Database;

public sealed class TenantUniquenessSqlServerIntegrationTests : IAsyncLifetime
{
    private MsSqlContainer? _sqlContainer;
    private string _connectionString = string.Empty;

    public async Task InitializeAsync()
    {
        _sqlContainer = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .WithPassword("Password123!")
            .Build();

        await _sqlContainer.StartAsync();
        _connectionString = _sqlContainer.GetConnectionString();
    }

    public async Task DisposeAsync()
    {
        if (_sqlContainer is not null)
        {
            await _sqlContainer.DisposeAsync();
        }
    }

    [Fact]
    public async Task Tenants_Should_Enforce_Unique_CnpjNormalized_And_ExternalIdentifier()
    {
        var options = MigrationTestSupport.CreateSqlServerOptions<TenancyDbContext>(
            _connectionString,
            "tenancy");

        await using var db = new TenancyDbContext(options);
        DatabaseMigrationRunner.ApplyMigrations(db, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

        var now = DateTime.UtcNow;
        db.Tenants.Add(new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Farm A",
            CNPJ = "12.345.678/0001-90",
            CnpjNormalized = "12345678000190",
            ExternalIdentifier = "CRM-001",
            State = "MT",
            City = "Sinop",
            Status = "Active",
            SubscribedPlan = "Starter",
            Capacity = 500,
            StateRegistration = "IE",
            Type = "Corte",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        await db.SaveChangesAsync();

        db.Tenants.Add(new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Farm B",
            CNPJ = "12.345.678/0001-90",
            CnpjNormalized = "12345678000190",
            State = "MT",
            City = "Sinop",
            Status = "Active",
            SubscribedPlan = "Starter",
            Capacity = 500,
            StateRegistration = "IE",
            Type = "Corte",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        var cnpjAct = async () => await db.SaveChangesAsync();
        await cnpjAct.Should().ThrowAsync<DbUpdateException>();

        db.ChangeTracker.Clear();
        db.Tenants.Add(new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Farm C",
            CNPJ = "98.765.432/0001-10",
            CnpjNormalized = "98765432000110",
            ExternalIdentifier = "CRM-001",
            State = "RS",
            City = "POA",
            Status = "Active",
            SubscribedPlan = "Starter",
            Capacity = 500,
            StateRegistration = "IE",
            Type = "Corte",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        var extAct = async () => await db.SaveChangesAsync();
        await extAct.Should().ThrowAsync<DbUpdateException>();
    }
}
