using CriaCerto.BuildingBlocks.Abstractions.Tenancy;
using CriaCerto.BuildingBlocks.Application.Abstractions;
using CriaCerto.BuildingBlocks.Infrastructure.Persistence;
using CriaCerto.BuildingBlocks.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CriaCerto.BuildingBlocks.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBuildingBlocksInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ITenantContext, HttpContextTenantContext>();
        services.AddScoped<ITenantConnectionProvider, TenantConnectionProvider>();
        services.AddSingleton<ITenantDatabaseProvisioner, TenantDatabaseProvisioner>();

        services.AddDbContextPool<FoundationDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sqlServerOptions =>
            {
                sqlServerOptions.ConfigureModuleMigrations<FoundationDbContext>("foundation");
            });

            options.EnableDetailedErrors();
        });

        services.AddScoped<IFoundationDbContext>(sp => sp.GetRequiredService<FoundationDbContext>());

        return services;
    }
}