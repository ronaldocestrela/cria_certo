using CriaCerto.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Architecture.IntegrationTests.Database;

internal static class MigrationTestSupport
{
    public static async Task ApplyBaselineAsync(string connectionString, IEnumerable<MigrationBaselineModule> modules, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var module in modules)
        {
            foreach (var table in module.RequiredTables)
            {
                await using var validateCommand = connection.CreateCommand();
                validateCommand.CommandText = """
                    SELECT COUNT(1)
                    FROM sys.tables t
                    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                    WHERE s.name = @schema AND t.name = @table
                    """;
                validateCommand.Parameters.AddWithValue("@schema", module.Schema);
                validateCommand.Parameters.AddWithValue("@table", table);

                var exists = (int)(await validateCommand.ExecuteScalarAsync(cancellationToken) ?? 0);
                if (exists == 0)
                {
                    throw new InvalidOperationException($"Missing table {module.Schema}.{table} required for baseline.");
                }
            }

            await using var ensureSchema = connection.CreateCommand();
            ensureSchema.CommandText = $"""
                IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'{module.Schema}')
                    EXEC(N'CREATE SCHEMA [{module.Schema}]');
                """;
            await ensureSchema.ExecuteNonQueryAsync(cancellationToken);

            await using var ensureHistory = connection.CreateCommand();
            ensureHistory.CommandText = $"""
                IF OBJECT_ID(N'[{module.Schema}].[{MigrationBaselineMetadata.HistoryTableName}]') IS NULL
                BEGIN
                    CREATE TABLE [{module.Schema}].[{MigrationBaselineMetadata.HistoryTableName}] (
                        [MigrationId] nvarchar(150) NOT NULL,
                        [ProductVersion] nvarchar(32) NOT NULL,
                        CONSTRAINT [PK_{module.Schema}_{MigrationBaselineMetadata.HistoryTableName}] PRIMARY KEY ([MigrationId])
                    );
                END
                """;
            await ensureHistory.ExecuteNonQueryAsync(cancellationToken);

            await using var insertHistory = connection.CreateCommand();
            insertHistory.CommandText = $"""
                IF NOT EXISTS (
                    SELECT 1 FROM [{module.Schema}].[{MigrationBaselineMetadata.HistoryTableName}]
                    WHERE [MigrationId] = @migrationId)
                BEGIN
                    INSERT INTO [{module.Schema}].[{MigrationBaselineMetadata.HistoryTableName}]
                        ([MigrationId], [ProductVersion])
                    VALUES (@migrationId, @productVersion);
                END
                """;
            insertHistory.Parameters.AddWithValue("@migrationId", module.MigrationId);
            insertHistory.Parameters.AddWithValue("@productVersion", MigrationBaselineMetadata.ProductVersion);
            await insertHistory.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public static async Task<int> CountHistoryRowsAsync(
        string connectionString,
        string schema,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT COUNT(1)
            FROM [{schema}].[{MigrationBaselineMetadata.HistoryTableName}]
            """;
        return (int)(await command.ExecuteScalarAsync(cancellationToken) ?? 0);
    }

    public static DbContextOptions<TContext> CreateSqlServerOptions<TContext>(
        string connectionString,
        string historySchema)
        where TContext : DbContext
    {
        var optionsBuilder = new DbContextOptionsBuilder<TContext>();
        optionsBuilder.UseSqlServer(connectionString, sql =>
            sql.ConfigureModuleMigrations<TContext>(historySchema));

        return optionsBuilder.Options;
    }
}
