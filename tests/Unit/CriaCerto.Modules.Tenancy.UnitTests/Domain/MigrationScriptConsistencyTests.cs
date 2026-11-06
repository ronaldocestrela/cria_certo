using System;
using System.IO;
using CriaCerto.Modules.Tenancy.Application.Domain;
using FluentAssertions;
using Xunit;

namespace CriaCerto.Modules.Tenancy.UnitTests.Domain;

public class MigrationScriptConsistencyTests
{
    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "CriaCerto.slnx")) ||
                File.Exists(Path.Combine(dir.FullName, "Directory.Build.props")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    [Fact]
    public void StandaloneMigrationScript_ShouldExist_And_BeNonEmpty()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "scripts", "database", "migrations", "20261105_migrate_existing_tenants_to_modular.sql");

        File.Exists(scriptPath).Should().BeTrue($"Expected script to exist at {scriptPath}");
        var content = File.ReadAllText(scriptPath);
        content.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void StandaloneMigrationScript_ShouldContainAllTargetTiersAndCoreModules()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "scripts", "database", "migrations", "20261105_migrate_existing_tenants_to_modular.sql");

        var content = File.ReadAllText(scriptPath);

        // Deve conter as 3 faixas alvos da migração
        content.Should().Contain(HeadTierCapacityLimits.Tier500);
        content.Should().Contain(HeadTierCapacityLimits.Tier3000);
        content.Should().Contain(HeadTierCapacityLimits.Tier5000Plus);

        // Deve conter as capacidades exatas
        content.Should().Contain("500");
        content.Should().Contain("3000");
        content.Should().Contain("10000");

        // Deve conter os módulos chave
        content.Should().Contain("'Cows'");
        content.Should().Contain("'Breeding'");
        content.Should().Contain("'Calving'");
        content.Should().Contain("'Growth'");
        content.Should().Contain("'Nutrition'");
        content.Should().Contain("'Sanitary'");
        content.Should().Contain("'Analytics'");

        // Deve conter cláusulas defensivas de idempotência
        content.Should().Contain("NOT EXISTS");
        content.Should().Contain("TenantSubscribedModules");
        content.Should().Contain("TenantSubscriptionHistories");
    }

    [Fact]
    public void EfCoreMigrationClass_ShouldExist_And_ExecuteSqlMigration()
    {
        var root = FindRepositoryRoot();
        var migrationFiles = Directory.GetFiles(
            Path.Combine(root, "src", "Modules", "Tenancy", "CriaCerto.Modules.Tenancy.Infrastructure", "Persistence", "Migrations"),
            "*MigrateExistingTenantsToModularCatalog.cs");

        migrationFiles.Should().HaveCount(1);
        var migrationContent = File.ReadAllText(migrationFiles[0]);

        migrationContent.Should().Contain("migrationBuilder.Sql");
        migrationContent.Should().Contain(HeadTierCapacityLimits.Tier500);
        migrationContent.Should().Contain(HeadTierCapacityLimits.Tier3000);
        migrationContent.Should().Contain(HeadTierCapacityLimits.Tier5000Plus);
        migrationContent.Should().Contain("'Cows'");
        migrationContent.Should().Contain("NOT EXISTS");
    }
}
