using System.Collections.Concurrent;
using CriaCerto.BuildingBlocks.Abstractions.Tenancy;
using CriaCerto.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CriaCerto.BuildingBlocks.Infrastructure.Tenancy;

public sealed class TenantDatabaseProvisioner : ITenantDatabaseProvisioner
{
    private static readonly ConcurrentDictionary<Guid, bool> ProvisionedTenants = new();
    private static readonly SemaphoreSlim Semaphore = new(1, 1);

    private readonly IConfiguration _configuration;
    private readonly ILogger<TenantDatabaseProvisioner> _logger;
    private readonly Dictionary<Type, string> _tenantDbContextTypes = new();

    public TenantDatabaseProvisioner(IConfiguration configuration, ILogger<TenantDatabaseProvisioner> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public void RegisterTenantDbContextType(Type dbContextType, string historySchema)
    {
        if (!typeof(DbContext).IsAssignableFrom(dbContextType))
        {
            throw new ArgumentException($"Tipo {dbContextType.Name} não é um DbContext.", nameof(dbContextType));
        }

        if (string.IsNullOrWhiteSpace(historySchema))
        {
            throw new ArgumentException("Schema de histórico de migrations é obrigatório.", nameof(historySchema));
        }

        _tenantDbContextTypes[dbContextType] = historySchema;
    }

    public async Task EnsureTenantDatabaseAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (ProvisionedTenants.ContainsKey(tenantId))
        {
            return;
        }

        await Semaphore.WaitAsync(cancellationToken);
        try
        {
            if (ProvisionedTenants.ContainsKey(tenantId))
            {
                return;
            }

            _logger.LogInformation("Garantindo inicialização do banco de dados para tenant {TenantId}...", tenantId);

            var baseConnectionString = _configuration.GetConnectionString("SqlServer")
                ?? _configuration.GetConnectionString("DefaultConnection")
                ?? "Server=localhost,1433;User Id=sa;Password=Password123!;TrustServerCertificate=True;Encrypt=False";

            var tenantBuilder = new SqlConnectionStringBuilder(baseConnectionString)
            {
                InitialCatalog = $"criacerto_tenant_{tenantId:N}"
            };
            var tenantConnectionString = tenantBuilder.ConnectionString;

            foreach (var (dbContextType, historySchema) in _tenantDbContextTypes)
            {
                var optionsBuilderType = typeof(DbContextOptionsBuilder<>).MakeGenericType(dbContextType);
                var optionsBuilder = (DbContextOptionsBuilder)Activator.CreateInstance(optionsBuilderType)!;

                ConfigureTenantDbContextOptions(optionsBuilder, dbContextType, historySchema, tenantConnectionString);

                await using var dbContext = (DbContext)Activator.CreateInstance(dbContextType, optionsBuilder.Options)!;
                await DatabaseMigrationRunner.ApplyMigrationsAsync(dbContext, _logger, cancellationToken);
            }

            ProvisionedTenants.TryAdd(tenantId, true);
            _logger.LogInformation("Inicialização do banco de dados do tenant {TenantId} concluída.", tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao provisionar banco de dados para tenant {TenantId}.", tenantId);
            throw;
        }
        finally
        {
            Semaphore.Release();
        }
    }

    private static void ConfigureTenantDbContextOptions(
        DbContextOptionsBuilder optionsBuilder,
        Type dbContextType,
        string historySchema,
        string connectionString)
    {
        optionsBuilder.UseSqlServer(connectionString, sqlOptions =>
        {
            var configureMethod = typeof(SqlServerMigrationExtensions)
                .GetMethod(nameof(SqlServerMigrationExtensions.ConfigureModuleMigrations))!
                .MakeGenericMethod(dbContextType);

            configureMethod.Invoke(null, [sqlOptions, historySchema]);
        });
    }
}
