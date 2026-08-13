using CriaCerto.BuildingBlocks.Abstractions.ReferenceData;
using CriaCerto.BuildingBlocks.Infrastructure.Persistence;
using CriaCerto.Modules.Breeding.Infrastructure.Persistence;
using CriaCerto.Modules.Tenancy.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.MsSql;

namespace CriaCerto.Architecture.IntegrationTests.Database;

public sealed class DatabaseMigrationIntegrationTests : IAsyncLifetime
{
    private MsSqlContainer? _sqlContainer;
    private string _masterConnectionString = string.Empty;

    public async Task InitializeAsync()
    {
        _sqlContainer = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .WithPassword("Password123!")
            .Build();

        await _sqlContainer.StartAsync();
        _masterConnectionString = _sqlContainer.GetConnectionString();
    }

    public async Task DisposeAsync()
    {
        if (_sqlContainer is not null)
        {
            await _sqlContainer.DisposeAsync();
        }
    }

    [Fact]
    public async Task FreshDatabase_Migrate_CreatesSchemasHistoryAndTables()
    {
        var foundationOptions = MigrationTestSupport.CreateSqlServerOptions<FoundationDbContext>(
            _masterConnectionString,
            MigrationBaselineMetadata.Foundation.Schema);

        await using (var foundationDb = new FoundationDbContext(foundationOptions))
        {
            DatabaseMigrationRunner.ApplyMigrations(foundationDb, NullLogger.Instance);
        }

        await using var connection = new SqlConnection(_masterConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(1)
            FROM sys.tables t
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            WHERE s.name = 'foundation' AND t.name = 'bovine_breeds'
            """;
        var tableExists = (int)(await command.ExecuteScalarAsync() ?? 0);

        tableExists.Should().Be(1);
        (await MigrationTestSupport.CountHistoryRowsAsync(_masterConnectionString, "foundation"))
            .Should().Be(1);
    }

    [Fact]
    public async Task SecondMigrate_IsIdempotent()
    {
        var options = MigrationTestSupport.CreateSqlServerOptions<FoundationDbContext>(
            _masterConnectionString,
            MigrationBaselineMetadata.Foundation.Schema);

        await using var dbContext = new FoundationDbContext(options);
        DatabaseMigrationRunner.ApplyMigrations(dbContext, NullLogger.Instance);
        DatabaseMigrationRunner.ApplyMigrations(dbContext, NullLogger.Instance);

        (await MigrationTestSupport.CountHistoryRowsAsync(_masterConnectionString, "foundation"))
            .Should().Be(1);
    }

    [Fact]
    public async Task Baseline_PreservesExistingData_AndAllowsIdempotentMigrate()
    {
        var databaseName = $"baseline_{Guid.NewGuid():N}";
        var connectionString = new SqlConnectionStringBuilder(_masterConnectionString)
        {
            InitialCatalog = databaseName
        }.ConnectionString;

        await using (var connection = new SqlConnection(_masterConnectionString))
        {
            await connection.OpenAsync();
            await using var createDb = connection.CreateCommand();
            createDb.CommandText = $"CREATE DATABASE [{databaseName}]";
            await createDb.ExecuteNonQueryAsync();
        }

        var legacyOptions = MigrationTestSupport.CreateSqlServerOptions<FoundationDbContext>(
            connectionString,
            MigrationBaselineMetadata.Foundation.Schema);

        var breedId = Guid.NewGuid();
        await using (var legacyDb = new FoundationDbContext(legacyOptions))
        {
            await legacyDb.Database.ExecuteSqlRawAsync("""
                IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'foundation') EXEC('CREATE SCHEMA [foundation]');
                CREATE TABLE [foundation].[bovine_breeds] (
                    [Id] uniqueidentifier NOT NULL,
                    [Code] nvarchar(20) NOT NULL,
                    [Name] nvarchar(100) NOT NULL,
                    [Category] nvarchar(50) NOT NULL,
                    [Aptitude] nvarchar(50) NOT NULL,
                    [Origin] nvarchar(50) NOT NULL,
                    [IsOfficial] bit NOT NULL,
                    CONSTRAINT [PK_bovine_breeds] PRIMARY KEY ([Id])
                );
                """);

            legacyDb.BovineBreeds.Add(new BovineBreed(
                breedId,
                "NELORE",
                "Nelore",
                "Corte",
                "Corte",
                "Brasil"));
            await legacyDb.SaveChangesAsync();
        }

        await using (var migratedDb = new FoundationDbContext(legacyOptions))
        {
            DatabaseMigrationRunner.ApplyMigrations(migratedDb, NullLogger.Instance);
            var preserved = await migratedDb.BovineBreeds.SingleAsync(b => b.Id == breedId);
            preserved.Code.Should().Be("NELORE");
        }

        (await MigrationTestSupport.CountHistoryRowsAsync(connectionString, "foundation"))
            .Should().Be(1);
    }

    [Fact]
    public async Task Baseline_FailsWhenRequiredTableIsMissing()
    {
        var databaseName = $"drift_{Guid.NewGuid():N}";
        var connectionString = new SqlConnectionStringBuilder(_masterConnectionString)
        {
            InitialCatalog = databaseName
        }.ConnectionString;

        await using (var connection = new SqlConnection(_masterConnectionString))
        {
            await connection.OpenAsync();
            await using var createDb = connection.CreateCommand();
            createDb.CommandText = $"CREATE DATABASE [{databaseName}]";
            await createDb.ExecuteNonQueryAsync();
        }

        var options = MigrationTestSupport.CreateSqlServerOptions<FoundationDbContext>(
            connectionString,
            MigrationBaselineMetadata.Foundation.Schema);

        await using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await using var createDrift = connection.CreateCommand();
            createDrift.CommandText = """
                IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'foundation')
                    EXEC('CREATE SCHEMA [foundation]');
                CREATE TABLE [foundation].[bovine_breeds] (
                    [Id] uniqueidentifier NOT NULL,
                    CONSTRAINT [PK_bovine_breeds] PRIMARY KEY ([Id])
                );
                """;
            await createDrift.ExecuteNonQueryAsync();
        }

        var act = () =>
        {
            using var dbContext = new FoundationDbContext(options);
            DatabaseMigrationRunner.ApplyMigrations(dbContext, NullLogger.Instance);
        };

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*missing column [foundation].[bovine_breeds]*");
    }

    [Fact]
    public async Task TenantProvisioner_Migrate_IsIdempotent()
    {
        var tenantId = Guid.NewGuid();
        var tenantConnectionString = new SqlConnectionStringBuilder(_masterConnectionString)
        {
            InitialCatalog = $"criacerto_tenant_{tenantId:N}"
        }.ConnectionString;

        var options = MigrationTestSupport.CreateSqlServerOptions<BreedingDbContext>(
            tenantConnectionString,
            MigrationBaselineMetadata.Breeding.Schema);

        await using var dbContext = new BreedingDbContext(options);
        await DatabaseMigrationRunner.ApplyMigrationsAsync(dbContext, NullLogger.Instance);
        await DatabaseMigrationRunner.ApplyMigrationsAsync(dbContext, NullLogger.Instance);

        (await MigrationTestSupport.CountHistoryRowsAsync(tenantConnectionString, "breeding"))
            .Should().Be(1);
    }

    [Fact]
    public async Task LegacyBreedingTables_AreBaselinedBeforeMigrate()
    {
        var databaseName = $"legacy_breeding_{Guid.NewGuid():N}";
        var connectionString = new SqlConnectionStringBuilder(_masterConnectionString)
        {
            InitialCatalog = databaseName
        }.ConnectionString;
        var options = MigrationTestSupport.CreateSqlServerOptions<BreedingDbContext>(
            connectionString,
            MigrationBaselineMetadata.Breeding.Schema);

        await using (var legacyDb = new BreedingDbContext(options))
        {
            (await legacyDb.Database.EnsureCreatedAsync()).Should().BeTrue();
            await legacyDb.Database.ExecuteSqlRawAsync("""
                ALTER TABLE [breeding].[Bulls]
                    ALTER COLUMN [BirthDate] datetime2 NOT NULL;
                ALTER TABLE [breeding].[Cows]
                    ALTER COLUMN [BirthDate] datetime2 NOT NULL;
                """);
        }

        await using (var migratedDb = new BreedingDbContext(options))
        {
            await DatabaseMigrationRunner.ApplyMigrationsAsync(migratedDb, NullLogger.Instance);
        }

        (await MigrationTestSupport.CountHistoryRowsAsync(connectionString, "breeding"))
            .Should().Be(1);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(1)
            FROM sys.columns c
            INNER JOIN sys.tables t ON c.object_id = t.object_id
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            WHERE s.name = 'breeding'
              AND t.name IN ('Bulls', 'Cows')
              AND c.name = 'BirthDate'
              AND c.is_nullable = 1
            """;
        Convert.ToInt32(await command.ExecuteScalarAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Tenancy_Migrate_CreatesUsersTable()
    {
        var options = MigrationTestSupport.CreateSqlServerOptions<TenancyDbContext>(
            _masterConnectionString,
            MigrationBaselineMetadata.Tenancy.Schema);

        await using var dbContext = new TenancyDbContext(options);
        DatabaseMigrationRunner.ApplyMigrations(dbContext, NullLogger.Instance);

        await using var connection = new SqlConnection(_masterConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(1)
            FROM sys.tables t
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            WHERE s.name = 'tenancy' AND t.name = 'Users'
            """;
        var tableExists = (int)(await command.ExecuteScalarAsync() ?? 0);
        tableExists.Should().Be(1);
    }
}
