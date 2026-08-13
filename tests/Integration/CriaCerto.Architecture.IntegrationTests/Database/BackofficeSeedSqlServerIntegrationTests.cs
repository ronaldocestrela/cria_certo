using CriaCerto.Api.Seeders;
using CriaCerto.BuildingBlocks.Infrastructure.Persistence;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Security;
using CriaCerto.Modules.Backoffice.Infrastructure.Persistence;
using CriaCerto.Modules.Backoffice.Infrastructure.Persistence.Seeders;
using CriaCerto.Modules.Backoffice.Infrastructure.Security;
using CriaCerto.Modules.Sanitary.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.MsSql;

namespace CriaCerto.Architecture.IntegrationTests.Database;

public sealed class BackofficeSeedSqlServerIntegrationTests : IAsyncLifetime
{
    private MsSqlContainer? _sqlContainer;
    private string _connectionString = string.Empty;
    private readonly PasswordHasherService _passwordHasher = new();

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
    public async Task MigrateAndSeed_OnEmptyDatabase_PopulatesBackofficeCatalog()
    {
        var foundationOptions = MigrationTestSupport.CreateSqlServerOptions<FoundationDbContext>(
            _connectionString,
            MigrationBaselineMetadata.Foundation.Schema);
        var sanitaryOptions = MigrationTestSupport.CreateSqlServerOptions<SanitaryDbContext>(
            _connectionString,
            MigrationBaselineMetadata.Sanitary.Schema);
        var backofficeOptions = MigrationTestSupport.CreateSqlServerOptions<BackofficeDbContext>(
            _connectionString,
            MigrationBaselineMetadata.Backoffice.Schema);

        await using var foundationDb = new FoundationDbContext(foundationOptions);
        await using var sanitaryDb = new SanitaryDbContext(sanitaryOptions);
        await using var backofficeDb = new BackofficeDbContext(backofficeOptions);

        DatabaseMigrationRunner.ApplyMigrations(foundationDb, NullLogger.Instance);
        DatabaseMigrationRunner.ApplyMigrations(sanitaryDb, NullLogger.Instance);
        DatabaseMigrationRunner.ApplyMigrations(backofficeDb, NullLogger.Instance);

        await SystemDataSeeder.SeedDataAsync(
            foundationDb,
            sanitaryDb,
            backofficeDb,
            _passwordHasher,
            NullLogger.Instance);

        var permissions = await backofficeDb.Permissions.ToListAsync();
        permissions.Select(p => p.Name).Should().Contain(BackofficePermissions.AllPermissions);

        var roles = await backofficeDb.AdminRoles.Include(r => r.Permissions).ToListAsync();
        roles.Select(r => r.Name).Should().Contain(BackofficeRoles.AllRoles);

        var masterAdmin = await backofficeDb.AdminUsers
            .Include(u => u.Roles)
            .SingleAsync(u => u.Email == BackofficeDataSeeder.MasterAdminEmail);

        masterAdmin.Roles.Should().Contain(r => r.Name == BackofficeRoles.PlatformOwner);
        _passwordHasher.VerifyPassword(BackofficeDataSeeder.MasterAdminPassword, masterAdmin.PasswordHash)
            .Should().BeTrue();
    }

    [Fact]
    public async Task Seed_IsIdempotent_OnSqlServer()
    {
        var backofficeOptions = MigrationTestSupport.CreateSqlServerOptions<BackofficeDbContext>(
            _connectionString,
            MigrationBaselineMetadata.Backoffice.Schema);

        await using var backofficeDb = new BackofficeDbContext(backofficeOptions);
        DatabaseMigrationRunner.ApplyMigrations(backofficeDb, NullLogger.Instance);

        await BackofficeDataSeeder.SeedAsync(backofficeDb, _passwordHasher, NullLogger.Instance);
        await BackofficeDataSeeder.SeedAsync(backofficeDb, _passwordHasher, NullLogger.Instance);

        (await backofficeDb.Permissions.CountAsync()).Should().Be(BackofficePermissions.AllPermissions.Count);
        (await backofficeDb.AdminRoles.CountAsync()).Should().Be(BackofficeRoles.AllRoles.Count);
        (await backofficeDb.AdminUsers.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Seed_WhenAnotherAdminExists_StillCreatesBootstrapAdmin()
    {
        var backofficeOptions = MigrationTestSupport.CreateSqlServerOptions<BackofficeDbContext>(
            _connectionString,
            MigrationBaselineMetadata.Backoffice.Schema);

        await using var backofficeDb = new BackofficeDbContext(backofficeOptions);
        DatabaseMigrationRunner.ApplyMigrations(backofficeDb, NullLogger.Instance);

        var otherAdmin = AdminUser.Create(
            "Outro Admin",
            "other.admin@criacerto.com.br",
            _passwordHasher.HashPassword("OtherPassword123!")).Value;
        backofficeDb.AdminUsers.Add(otherAdmin);
        await backofficeDb.SaveChangesAsync();

        await BackofficeDataSeeder.SeedAsync(backofficeDb, _passwordHasher, NullLogger.Instance);

        var adminUsers = await backofficeDb.AdminUsers
            .Include(u => u.Roles)
            .ToListAsync();

        adminUsers.Should().HaveCount(2);
        adminUsers.Should().Contain(u => u.Email == BackofficeDataSeeder.MasterAdminEmail);
        adminUsers.Single(u => u.Email == BackofficeDataSeeder.MasterAdminEmail)
            .Roles.Should().Contain(r => r.Name == BackofficeRoles.PlatformOwner);
    }

    [Fact]
    public async Task Seed_WhenBootstrapAdminExistsWithoutRole_RepairsPlatformOwnerAssignment()
    {
        var backofficeOptions = MigrationTestSupport.CreateSqlServerOptions<BackofficeDbContext>(
            _connectionString,
            MigrationBaselineMetadata.Backoffice.Schema);

        await using var backofficeDb = new BackofficeDbContext(backofficeOptions);
        DatabaseMigrationRunner.ApplyMigrations(backofficeDb, NullLogger.Instance);

        await BackofficeDataSeeder.SeedAsync(backofficeDb, _passwordHasher, NullLogger.Instance);

        var masterAdmin = await backofficeDb.AdminUsers
            .Include(u => u.Roles)
            .SingleAsync(u => u.Email == BackofficeDataSeeder.MasterAdminEmail);

        foreach (var role in masterAdmin.Roles.ToList())
        {
            masterAdmin.RemoveRole(role.Id);
        }

        await backofficeDb.SaveChangesAsync();

        await BackofficeDataSeeder.SeedAsync(backofficeDb, _passwordHasher, NullLogger.Instance);

        var repairedAdmin = await backofficeDb.AdminUsers
            .Include(u => u.Roles)
            .SingleAsync(u => u.Email == BackofficeDataSeeder.MasterAdminEmail);

        repairedAdmin.Roles.Should().Contain(r => r.Name == BackofficeRoles.PlatformOwner);
    }
}
