using CriaCerto.BuildingBlocks.Infrastructure.Persistence;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Features.BackofficeTenants;
using CriaCerto.Modules.Tenancy.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace CriaCerto.Architecture.IntegrationTests.Database;

public sealed class TenantSegmentationSqlServerIntegrationTests : IAsyncLifetime
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
    public async Task FilteredQuery_On_5000_Tenants_Should_Complete_Under_500ms()
    {
        var options = MigrationTestSupport.CreateSqlServerOptions<TenancyDbContext>(_connectionString, "tenancy");
        await using var db = new TenancyDbContext(options);
        DatabaseMigrationRunner.ApplyMigrations(db, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

        var tagId = Guid.NewGuid();
        db.OperationalTags.Add(new OperationalTag
        {
            Id = tagId,
            Name = "Retention High",
            Slug = "retention-high",
            Category = TenantSegmentationCatalog.TagCategories.Retention,
            ColorHex = "#6366f1",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        });

        var now = DateTime.UtcNow;
        for (var i = 0; i < 5000; i++)
        {
            var tenant = new Tenant
            {
                Id = Guid.NewGuid(),
                Name = $"Farm {i}",
                CNPJ = $"{i:00000000000000}",
                CnpjNormalized = $"{i:00000000000000}",
                State = i % 2 == 0 ? "MT" : "SP",
                City = "City",
                Status = "Active",
                SubscribedPlan = "Starter",
                Capacity = 500,
                StateRegistration = "IE",
                Type = "Corte",
                CreatedAtUtc = now.AddMinutes(-i),
                UpdatedAtUtc = now.AddMinutes(-i)
            };
            tenant.ApplyDefaultSegmentation();
            tenant.ChurnRisk = i % 10 == 0
                ? TenantSegmentationCatalog.ChurnRisks.High
                : TenantSegmentationCatalog.ChurnRisks.None;
            tenant.CommercialRegion = i % 2 == 0
                ? TenantSegmentationCatalog.CommercialRegions.CentroOeste
                : TenantSegmentationCatalog.CommercialRegions.Sudeste;

            if (i % 10 == 0)
            {
                tenant.OperationalTags.Add(new TenantOperationalTag
                {
                    TenantId = tenant.Id,
                    TagId = tagId,
                    AssignedAtUtc = now
                });
            }

            db.Tenants.Add(tenant);
        }

        await db.SaveChangesAsync();

        var handler = new GetTenantsBackofficeQueryHandler(db);
        await handler.Handle(new GetTenantsBackofficeQuery(
            ChurnRisk: TenantSegmentationCatalog.ChurnRisks.High,
            CommercialRegion: TenantSegmentationCatalog.CommercialRegions.CentroOeste,
            TagIds: [tagId],
            PageSize: 50), CancellationToken.None);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await handler.Handle(new GetTenantsBackofficeQuery(
            ChurnRisk: TenantSegmentationCatalog.ChurnRisks.High,
            CommercialRegion: TenantSegmentationCatalog.CommercialRegions.CentroOeste,
            TagIds: [tagId],
            PageSize: 50), CancellationToken.None);
        sw.Stop();

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().BeGreaterThan(0);
        sw.ElapsedMilliseconds.Should().BeLessThan(500);
    }

    [Fact]
    public async Task KeysetPagination_Should_Not_Duplicate_Items()
    {
        var options = MigrationTestSupport.CreateSqlServerOptions<TenancyDbContext>(_connectionString, "tenancy");
        await using var db = new TenancyDbContext(options);
        DatabaseMigrationRunner.ApplyMigrations(db, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

        var now = DateTime.UtcNow;
        for (var i = 0; i < 25; i++)
        {
            var tenant = new Tenant
            {
                Id = Guid.NewGuid(),
                Name = $"Keyset {i}",
                CNPJ = $"{i + 60000:00000000000000}",
                CnpjNormalized = $"{i + 60000:00000000000000}",
                State = "MT",
                City = "City",
                Status = "Active",
                SubscribedPlan = "Starter",
                Capacity = 500,
                StateRegistration = "IE",
                Type = "Corte",
                CreatedAtUtc = now.AddMinutes(-i),
                UpdatedAtUtc = now.AddMinutes(-i)
            };
            tenant.ApplyDefaultSegmentation();
            db.Tenants.Add(tenant);
        }

        await db.SaveChangesAsync();

        var handler = new GetTenantsBackofficeQueryHandler(db);
        var collected = new List<Guid>();
        DateTime? cursorCreatedAt = null;
        Guid? cursorId = null;

        for (var page = 0; page < 10; page++)
        {
            var result = await handler.Handle(new GetTenantsBackofficeQuery(
                PageSize: 3,
                AfterCreatedAtUtc: cursorCreatedAt,
                AfterId: cursorId), CancellationToken.None);

            if (result.Value.Items.Count == 0)
            {
                break;
            }

            collected.AddRange(result.Value.Items.Select(x => x.Id));
            var last = result.Value.Items.Last();
            cursorCreatedAt = last.CreatedAtUtc;
            cursorId = last.Id;
        }

        collected.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Export_Should_Fail_When_Result_Exceeds_Limit()
    {
        var options = MigrationTestSupport.CreateSqlServerOptions<TenancyDbContext>(_connectionString, "tenancy");
        await using var db = new TenancyDbContext(options);
        DatabaseMigrationRunner.ApplyMigrations(db, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

        var now = DateTime.UtcNow;
        for (var i = 0; i < 10001; i++)
        {
            var tenant = new Tenant
            {
                Id = Guid.NewGuid(),
                Name = $"Export Farm {i}",
                CNPJ = $"{i + 70000:00000000000000}",
                CnpjNormalized = $"{i + 70000:00000000000000}",
                State = "MT",
                City = "City",
                Status = "Active",
                SubscribedPlan = "Starter",
                Capacity = 500,
                StateRegistration = "IE",
                Type = "Corte",
                CreatedAtUtc = now.AddMinutes(-i),
                UpdatedAtUtc = now.AddMinutes(-i)
            };
            tenant.ApplyDefaultSegmentation();
            db.Tenants.Add(tenant);
        }

        await db.SaveChangesAsync();

        var handler = new ExportTenantsBackofficeQueryHandler(db);
        var result = await handler.Handle(new ExportTenantsBackofficeQuery(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.ExportLimitExceeded");
    }
}
