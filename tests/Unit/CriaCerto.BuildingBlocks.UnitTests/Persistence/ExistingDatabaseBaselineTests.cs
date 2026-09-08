using CriaCerto.BuildingBlocks.Infrastructure.Persistence;
using CriaCerto.Modules.Tenancy.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CriaCerto.BuildingBlocks.UnitTests.Persistence;

public class ExistingDatabaseBaselineTests
{
    [Fact]
    public void GetExpectedBaselineColumns_ForTenancy_ReturnsOnlyInitialCreateColumns()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TenancyDbContext>()
            .UseSqlServer("Server=fake;Database=fake;TrustServerCertificate=True;", sql =>
            {
                sql.ConfigureModuleMigrations<TenancyDbContext>("tenancy");
            })
            .Options;

        using var dbContext = new TenancyDbContext(options);

        // Act
        var baselineColumns = ExistingDatabaseBaseline.GetExpectedBaselineColumns(
            dbContext,
            MigrationBaselineMetadata.Tenancy);

        // Assert
        baselineColumns.Should().NotBeEmpty();

        // Tables present in InitialCreate must be included
        var tables = baselineColumns.Select(c => c.Table).Distinct().ToList();
        tables.Should().Contain("Tenants");
        tables.Should().Contain("Users");
        tables.Should().Contain("ProductionUnits");
        tables.Should().Contain("TeamInvites");
        tables.Should().Contain("UserTenants");

        // Tables from subsequent migrations must NOT be included in baseline
        tables.Should().NotContain("OperationalTags");
        tables.Should().NotContain("TenantOperationalTags");
        tables.Should().NotContain("TenantSubscriptionHistories");

        // Columns from subsequent migrations must NOT be included in baseline
        var tenantColumns = baselineColumns.Where(c => c.Table == "Tenants").Select(c => c.Column).ToList();
        tenantColumns.Should().Contain("Id");
        tenantColumns.Should().Contain("Name");
        tenantColumns.Should().Contain("CNPJ");
        tenantColumns.Should().NotContain("SizeSegment");
        tenantColumns.Should().NotContain("IsBackofficeSuspended");
    }
}
