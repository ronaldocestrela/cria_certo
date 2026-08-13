using CriaCerto.BuildingBlocks.Abstractions.Tenancy;
using CriaCerto.BuildingBlocks.Infrastructure.Persistence;
using CriaCerto.Modules.Breeding.Application.Abstractions;
using CriaCerto.Modules.Breeding.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CriaCerto.Modules.Breeding.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBreedingInfrastructure(this IServiceCollection services)
    {
        services.AddDbContext<BreedingDbContext>((sp, options) =>
        {
            var connectionProvider = sp.GetRequiredService<ITenantConnectionProvider>();
            options.UseSqlServer(connectionProvider.GetConnectionString(), sqlServerOptions =>
            {
                sqlServerOptions.ConfigureModuleMigrations<BreedingDbContext>("breeding");
            });

            options.EnableDetailedErrors();
        });

        services.AddScoped<IBreedingDbContext>(sp => sp.GetRequiredService<BreedingDbContext>());
        return services;
    }
}
