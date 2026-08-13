using System.Collections.Concurrent;
using CriaCerto.BuildingBlocks.Abstractions.Tenancy;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CriaCerto.BuildingBlocks.Infrastructure.Tenancy;

public sealed class TenantDatabaseProvisioner : ITenantDatabaseProvisioner
{
    private static readonly ConcurrentDictionary<Guid, bool> _provisionedTenants = new();
    private static readonly SemaphoreSlim _semaphore = new(1, 1);

    private readonly IConfiguration _configuration;
    private readonly ILogger<TenantDatabaseProvisioner> _logger;
    private readonly List<Type> _tenantDbContextTypes = new();

    public TenantDatabaseProvisioner(IConfiguration configuration, ILogger<TenantDatabaseProvisioner> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public void RegisterTenantDbContextType(Type dbContextType)
    {
        if (!typeof(DbContext).IsAssignableFrom(dbContextType))
            throw new ArgumentException($"Tipo {dbContextType.Name} não é um DbContext.", nameof(dbContextType));

        if (!_tenantDbContextTypes.Contains(dbContextType))
        {
            _tenantDbContextTypes.Add(dbContextType);
        }
    }

    public async Task EnsureTenantDatabaseAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (_provisionedTenants.ContainsKey(tenantId))
            return;

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (_provisionedTenants.ContainsKey(tenantId))
                return;

            _logger.LogInformation("Garantindo inicialização do banco de dados para tenant {TenantId}...", tenantId);

            var baseConnectionString = _configuration.GetConnectionString("SqlServer")
                ?? _configuration.GetConnectionString("DefaultConnection")
                ?? "Server=localhost,1433;User Id=sa;Password=Password123!;TrustServerCertificate=True;Encrypt=False";

            var tenantBuilder = new SqlConnectionStringBuilder(baseConnectionString)
            {
                InitialCatalog = $"criacerto_tenant_{tenantId:N}"
            };
            var tenantConnectionString = tenantBuilder.ConnectionString;

            foreach (var dbContextType in _tenantDbContextTypes)
            {
                var optionsBuilderType = typeof(DbContextOptionsBuilder<>).MakeGenericType(dbContextType);
                var optionsBuilder = (DbContextOptionsBuilder)Activator.CreateInstance(optionsBuilderType)!;
                optionsBuilder.UseSqlServer(tenantConnectionString, sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
                });

                using var dbContext = (DbContext)Activator.CreateInstance(dbContextType, optionsBuilder.Options)!;

                if (dbContext.Database.IsRelational())
                {
                    var databaseCreator = dbContext.Database.GetService<IDatabaseCreator>() as RelationalDatabaseCreator;
                    if (databaseCreator != null)
                    {
                        if (!await databaseCreator.ExistsAsync(cancellationToken))
                        {
                            await databaseCreator.CreateAsync(cancellationToken);
                            _logger.LogInformation("Banco de dados criacerto_tenant_{TenantId:N} criado com sucesso.", tenantId);
                        }

                        try
                        {
                            await databaseCreator.CreateTablesAsync(cancellationToken);
                            _logger.LogInformation("Tabelas criadas para {DbContextName} no tenant {TenantId}.", dbContextType.Name, tenantId);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "Tabelas para {DbContextName} já existem no tenant {TenantId}.", dbContextType.Name, tenantId);
                        }

                        try
                        {
                            await EnsureBirthDateNullableAsync(dbContext, cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "Falha ao ajustar colunas nulas em {DbContextName} no tenant {TenantId}.", dbContextType.Name, tenantId);
                        }
                    }
                }
                else
                {
                    await dbContext.Database.EnsureCreatedAsync(cancellationToken);
                }
            }

            _provisionedTenants.TryAdd(tenantId, true);
            _logger.LogInformation("Inicialização do banco de dados do tenant {TenantId} concluída.", tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao provisionar banco de dados para tenant {TenantId}.", tenantId);
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private static async Task EnsureBirthDateNullableAsync(DbContext dbContext, CancellationToken cancellationToken)
    {
        var alterSql = @"
            IF EXISTS (
                SELECT 1 FROM sys.columns c
                JOIN sys.tables t ON c.object_id = t.object_id
                JOIN sys.schemas s ON t.schema_id = s.schema_id
                WHERE s.name = 'breeding' AND t.name = 'Cows' AND c.name = 'BirthDate' AND c.is_nullable = 0
            )
            BEGIN
                ALTER TABLE [breeding].[Cows] ALTER COLUMN [BirthDate] DATETIME2 NULL;
            END;

            IF EXISTS (
                SELECT 1 FROM sys.columns c
                JOIN sys.tables t ON c.object_id = t.object_id
                JOIN sys.schemas s ON t.schema_id = s.schema_id
                WHERE s.name = 'breeding' AND t.name = 'Bulls' AND c.name = 'BirthDate' AND c.is_nullable = 0
            )
            BEGIN
                ALTER TABLE [breeding].[Bulls] ALTER COLUMN [BirthDate] DATETIME2 NULL;
            END;";

        await dbContext.Database.ExecuteSqlRawAsync(alterSql, cancellationToken);
    }
}
