using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Features.BackofficeTenants;
using CriaCerto.Modules.Tenancy.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Tenancy.UnitTests;

public class TenantSegmentationHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TenancyDbContext _dbContext;

    public TenantSegmentationHandlerTests()
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
    public async Task UpdateSegmentation_Should_Persist_Values()
    {
        var tenantId = await SeedTenantAsync();
        var handler = new UpdateTenantSegmentationForAdminCommandHandler(_dbContext);

        var result = await handler.Handle(new UpdateTenantSegmentationForAdminCommand(
            tenantId,
            TenantSegmentationCatalog.SizeSegments.Large,
            TenantSegmentationCatalog.CommercialRegions.Sudeste,
            TenantSegmentationCatalog.ProductiveProfiles.Engorda,
            TenantSegmentationCatalog.ChurnRisks.High), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ChurnRisk.Should().Be(TenantSegmentationCatalog.ChurnRisks.High);
    }

    [Fact]
    public async Task ReplaceTags_Should_Be_Idempotent()
    {
        var tenantId = await SeedTenantAsync();
        var tag = new OperationalTag
        {
            Id = Guid.NewGuid(),
            Name = "CS Risco",
            Slug = "cs-risco",
            Category = TenantSegmentationCatalog.TagCategories.CustomerSuccess,
            ColorHex = "#6366f1",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        _dbContext.OperationalTags.Add(tag);
        await _dbContext.SaveChangesAsync();

        var handler = new ReplaceTenantTagsForAdminCommandHandler(_dbContext);
        var first = await handler.Handle(new ReplaceTenantTagsForAdminCommand(tenantId, [tag.Id]), CancellationToken.None);
        var second = await handler.Handle(new ReplaceTenantTagsForAdminCommand(tenantId, [tag.Id]), CancellationToken.None);

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        second.Value.Tags.Should().HaveCount(1);
    }

    [Fact]
    public async Task ReplaceTags_Should_Fail_When_Tag_Inactive()
    {
        var tenantId = await SeedTenantAsync();
        var tag = new OperationalTag
        {
            Id = Guid.NewGuid(),
            Name = "Inativa",
            Slug = "inativa",
            Category = TenantSegmentationCatalog.TagCategories.Support,
            ColorHex = "#000000",
            IsActive = false,
            CreatedAtUtc = DateTime.UtcNow
        };
        _dbContext.OperationalTags.Add(tag);
        await _dbContext.SaveChangesAsync();

        var handler = new ReplaceTenantTagsForAdminCommandHandler(_dbContext);
        var result = await handler.Handle(new ReplaceTenantTagsForAdminCommand(tenantId, [tag.Id]), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.TagInactive");
    }

    [Fact]
    public async Task GetTenants_Should_Clamp_PageSize()
    {
        await SeedTenantsAsync(3);
        var handler = new GetTenantsBackofficeQueryHandler(_dbContext);

        var result = await handler.Handle(new GetTenantsBackofficeQuery(PageSize: 500), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.PageSize.Should().Be(100);
    }

    [Fact]
    public async Task GetTenants_Should_Filter_By_ChurnRisk()
    {
        await SeedTenantsAsync(2, TenantSegmentationCatalog.ChurnRisks.High);
        await SeedTenantsAsync(1, TenantSegmentationCatalog.ChurnRisks.None);
        var handler = new GetTenantsBackofficeQueryHandler(_dbContext);

        var result = await handler.Handle(new GetTenantsBackofficeQuery(
            ChurnRisk: TenantSegmentationCatalog.ChurnRisks.High), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task CreateTag_Should_Fail_When_Slug_Exists()
    {
        _dbContext.OperationalTags.Add(new OperationalTag
        {
            Id = Guid.NewGuid(),
            Name = "Existing",
            Slug = "cs-risco-churn",
            Category = TenantSegmentationCatalog.TagCategories.Support,
            ColorHex = "#111111",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        var handler = new CreateOperationalTagForAdminCommandHandler(_dbContext);
        var result = await handler.Handle(new CreateOperationalTagForAdminCommand(
            "CS Risco Churn",
            TenantSegmentationCatalog.TagCategories.Support,
            null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.TagSlugAlreadyExists");
    }

    [Fact]
    public async Task Export_Should_Fail_When_Exceeding_Limit()
    {
        for (var i = 0; i < 10001; i++)
        {
            var tenant = CreateTenant($"Farm {i}", $"{i + 90000:00000000000000}", DateTime.UtcNow.AddMinutes(-i));
            _dbContext.Tenants.Add(tenant);
        }

        await _dbContext.SaveChangesAsync();

        var handler = new ExportTenantsBackofficeQueryHandler(_dbContext);
        var result = await handler.Handle(new ExportTenantsBackofficeQuery(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.ExportLimitExceeded");
    }

    private async Task<Guid> SeedTenantAsync()
    {
        var tenant = CreateTenant("Farm Seg", "12345678000190", DateTime.UtcNow);
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();
        return tenant.Id;
    }

    private async Task SeedTenantsAsync(int count, string churnRisk = TenantSegmentationCatalog.ChurnRisks.None)
    {
        for (var i = 0; i < count; i++)
        {
            var tenant = CreateTenant($"Tenant {Guid.NewGuid():N}", Guid.NewGuid().ToString("N")[..14], DateTime.UtcNow.AddMinutes(-i));
            tenant.ChurnRisk = churnRisk;
            _dbContext.Tenants.Add(tenant);
        }

        await _dbContext.SaveChangesAsync();
    }

    private static Tenant CreateTenant(string name, string cnpjNormalized, DateTime createdAt)
    {
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = name,
            CNPJ = cnpjNormalized,
            CnpjNormalized = cnpjNormalized,
            State = "MT",
            City = "Sinop",
            Status = "Active",
            SubscribedPlan = "Starter",
            Capacity = 500,
            StateRegistration = "IE",
            Type = "Corte",
            CreatedAtUtc = createdAt,
            UpdatedAtUtc = createdAt
        };
        tenant.ApplyDefaultSegmentation();
        return tenant;
    }
}
