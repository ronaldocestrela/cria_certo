using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;

namespace CriaCerto.BuildingBlocks.Infrastructure.Persistence;

internal static class ExistingDatabaseBaseline
{
    public static void ApplyIfNeeded(DbContext dbContext, ILogger logger)
    {
        ApplyIfNeededAsync(dbContext, logger).GetAwaiter().GetResult();
    }

    public static async Task ApplyIfNeededAsync(
        DbContext dbContext,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (!dbContext.Database.IsSqlServer()
            || !await dbContext.Database.CanConnectAsync(cancellationToken))
        {
            return;
        }

        var schema = dbContext.Model.GetDefaultSchema();
        var module = MigrationBaselineMetadata.FindBySchema(schema);
        if (module is null)
        {
            return;
        }

        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            if (await HasMigrationHistoryAsync(connection, module, cancellationToken))
            {
                return;
            }

            var existingTables = await GetExistingRequiredTablesAsync(connection, module, cancellationToken);
            if (existingTables.Count == 0)
            {
                return;
            }

            var missingTables = module.RequiredTables
                .Where(table => !existingTables.Contains(table))
                .ToArray();
            if (missingTables.Length > 0)
            {
                throw new InvalidOperationException(
                    $"Cannot baseline {dbContext.GetType().Name}: existing schema '{module.Schema}' is incomplete. " +
                    $"Missing tables: {string.Join(", ", missingTables)}.");
            }

            await ApplyKnownLegacyUpgradesAsync(connection, module, logger, cancellationToken);
            await ValidateModelColumnsAsync(dbContext, connection, module, cancellationToken);
            await InsertBaselineHistoryAsync(connection, module, cancellationToken);

            logger.LogInformation(
                "Existing schema {Schema} validated and baselined with migration {MigrationId}; business data was preserved.",
                module.Schema,
                module.MigrationId);
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task ApplyKnownLegacyUpgradesAsync(
        DbConnection connection,
        MigrationBaselineModule module,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        string? sql = module.Schema switch
        {
            "breeding" => """
                IF EXISTS (
                    SELECT 1
                    FROM sys.columns c
                    INNER JOIN sys.tables t ON c.object_id = t.object_id
                    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                    WHERE s.name = 'breeding'
                      AND t.name = 'Bulls'
                      AND c.name = 'BirthDate'
                      AND c.is_nullable = 0)
                BEGIN
                    ALTER TABLE [breeding].[Bulls]
                        ALTER COLUMN [BirthDate] datetime2 NULL;
                END;

                IF EXISTS (
                    SELECT 1
                    FROM sys.columns c
                    INNER JOIN sys.tables t ON c.object_id = t.object_id
                    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                    WHERE s.name = 'breeding'
                      AND t.name = 'Cows'
                      AND c.name = 'BirthDate'
                      AND c.is_nullable = 0)
                BEGIN
                    ALTER TABLE [breeding].[Cows]
                        ALTER COLUMN [BirthDate] datetime2 NULL;
                END;

                IF COL_LENGTH('breeding.IatfProtocols', 'BullId') IS NULL
                BEGIN
                    ALTER TABLE [breeding].[IatfProtocols]
                        ADD [BullId] uniqueidentifier NULL;
                END;

                IF COL_LENGTH('breeding.IatfProtocols', 'BullName') IS NULL
                BEGIN
                    ALTER TABLE [breeding].[IatfProtocols]
                        ADD [BullName] nvarchar(150) NULL;
                END;

                IF EXISTS (
                    SELECT 1 FROM sys.tables t
                    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                    WHERE s.name = 'breeding' AND t.name = 'Cows')
                BEGIN
                    UPDATE [breeding].[Cows]
                    SET [Status] = 'Active'
                    WHERE [Category] IN ('Reprodutor', 'Touro') AND [Status] = 'Open';
                END;
                """,
            "backoffice" => """
                IF COL_LENGTH('backoffice.AdminUsers', 'MfaSecretKey') IS NULL
                BEGIN
                    ALTER TABLE [backoffice].[AdminUsers]
                        ADD [MfaSecretKey] nvarchar(500) NULL;
                END;

                IF COL_LENGTH('backoffice.AdminUsers', 'MustChangePasswordOnNextLogin') IS NULL
                BEGIN
                    ALTER TABLE [backoffice].[AdminUsers]
                        ADD [MustChangePasswordOnNextLogin] bit NOT NULL
                            CONSTRAINT [DF_AdminUsers_MustChangePasswordOnNextLogin] DEFAULT 0;
                END;
                """,
            _ => null
        };

        if (sql is null)
        {
            return;
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            var affected = await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "Applied idempotent legacy compatibility checks for schema {Schema}; affected rows: {AffectedRows}.",
                module.Schema,
                affected);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static async Task<bool> HasMigrationHistoryAsync(
        DbConnection connection,
        MigrationBaselineModule module,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(1)
            FROM sys.tables t
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            WHERE s.name = @schema AND t.name = @historyTable
            """;
        AddParameter(command, "@schema", module.Schema);
        AddParameter(command, "@historyTable", MigrationBaselineMetadata.HistoryTableName);

        if (Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 0)
        {
            return false;
        }

        command.Parameters.Clear();
        command.CommandText = $"""
            SELECT COUNT(1)
            FROM [{module.Schema}].[{MigrationBaselineMetadata.HistoryTableName}]
            WHERE [MigrationId] = @migrationId
            """;
        AddParameter(command, "@migrationId", module.MigrationId);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    private static async Task<HashSet<string>> GetExistingRequiredTablesAsync(
        DbConnection connection,
        MigrationBaselineModule module,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT t.name
            FROM sys.tables t
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            WHERE s.name = @schema
            """;
        AddParameter(command, "@schema", module.Schema);

        var required = module.RequiredTables.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var table = reader.GetString(0);
            if (required.Contains(table))
            {
                existing.Add(table);
            }
        }

        return existing;
    }

    private static async Task ValidateModelColumnsAsync(
        DbContext dbContext,
        DbConnection connection,
        MigrationBaselineModule module,
        CancellationToken cancellationToken)
    {
        var validated = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entityType in dbContext.Model.GetEntityTypes())
        {
            var table = entityType.GetTableName();
            var schema = entityType.GetSchema() ?? dbContext.Model.GetDefaultSchema();
            if (table is null
                || !string.Equals(schema, module.Schema, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var storeObject = StoreObjectIdentifier.Table(table, schema);
            foreach (var property in entityType.GetProperties())
            {
                var column = property.GetColumnName(storeObject);
                if (column is null || !validated.Add($"{schema}.{table}.{column}"))
                {
                    continue;
                }

                var expectedStoreType = property.GetColumnType()
                    ?? property.GetRelationalTypeMapping().StoreType;
                var actual = await GetColumnDefinitionAsync(
                    connection,
                    schema!,
                    table,
                    column,
                    cancellationToken);

                if (actual is null)
                {
                    throw new InvalidOperationException(
                        $"Cannot baseline {dbContext.GetType().Name}: missing column [{schema}].[{table}].[{column}].");
                }

                if (!StoreTypesMatch(expectedStoreType, actual.Value.StoreType)
                    || property.IsColumnNullable(storeObject) != actual.Value.IsNullable)
                {
                    throw new InvalidOperationException(
                        $"Cannot baseline {dbContext.GetType().Name}: column [{schema}].[{table}].[{column}] " +
                        $"does not match the EF model. Expected {expectedStoreType} " +
                        $"{(property.IsColumnNullable(storeObject) ? "NULL" : "NOT NULL")}, found " +
                        $"{actual.Value.StoreType} {(actual.Value.IsNullable ? "NULL" : "NOT NULL")}.");
                }
            }
        }
    }

    private static async Task<(string StoreType, bool IsNullable)?> GetColumnDefinitionAsync(
        DbConnection connection,
        string schema,
        string table,
        string column,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                ty.name,
                c.max_length,
                c.precision,
                c.scale,
                c.is_nullable
            FROM sys.columns c
            INNER JOIN sys.tables t ON c.object_id = t.object_id
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
            WHERE s.name = @schema
              AND t.name = @table
              AND c.name = @column
            """;
        AddParameter(command, "@schema", schema);
        AddParameter(command, "@table", table);
        AddParameter(command, "@column", column);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var typeName = reader.GetString(0);
        var maxLength = reader.GetInt16(1);
        var precision = reader.GetByte(2);
        var scale = reader.GetByte(3);
        var nullable = reader.GetBoolean(4);

        var storeType = typeName.ToLowerInvariant() switch
        {
            "nvarchar" or "nchar" => $"{typeName}({(maxLength == -1 ? "max" : maxLength / 2)})",
            "varchar" or "char" or "varbinary" or "binary" =>
                $"{typeName}({(maxLength == -1 ? "max" : maxLength)})",
            "decimal" or "numeric" => $"{typeName}({precision},{scale})",
            _ => typeName
        };

        return (storeType, nullable);
    }

    private static bool StoreTypesMatch(string expected, string actual)
    {
        static string Normalize(string value) =>
            value.Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant();

        expected = Normalize(expected);
        actual = Normalize(actual);

        if (expected == actual)
        {
            return true;
        }

        // EF commonly omits SQL Server's default temporal precision.
        return expected is "datetime2" or "datetimeoffset" or "time"
            && actual.StartsWith(expected, StringComparison.Ordinal);
    }

    private static async Task InsertBaselineHistoryAsync(
        DbConnection connection,
        MigrationBaselineModule module,
        CancellationToken cancellationToken)
    {
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"""
                IF OBJECT_ID(N'[{module.Schema}].[{MigrationBaselineMetadata.HistoryTableName}]') IS NULL
                BEGIN
                    CREATE TABLE [{module.Schema}].[{MigrationBaselineMetadata.HistoryTableName}] (
                        [MigrationId] nvarchar(150) NOT NULL,
                        [ProductVersion] nvarchar(32) NOT NULL,
                        CONSTRAINT [PK_{module.Schema}_{MigrationBaselineMetadata.HistoryTableName}]
                            PRIMARY KEY ([MigrationId])
                    );
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM [{module.Schema}].[{MigrationBaselineMetadata.HistoryTableName}]
                    WHERE [MigrationId] = @migrationId)
                BEGIN
                    INSERT INTO [{module.Schema}].[{MigrationBaselineMetadata.HistoryTableName}]
                        ([MigrationId], [ProductVersion])
                    VALUES (@migrationId, @productVersion);
                END;
                """;
            AddParameter(command, "@migrationId", module.MigrationId);
            AddParameter(command, "@productVersion", MigrationBaselineMetadata.ProductVersion);
            await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
