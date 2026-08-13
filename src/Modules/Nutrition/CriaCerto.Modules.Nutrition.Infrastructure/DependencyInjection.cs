using CriaCerto.BuildingBlocks.Abstractions.Tenancy;
using CriaCerto.BuildingBlocks.Infrastructure.Persistence;
using CriaCerto.Modules.Nutrition.Application.Features.SiloStockFeatures;
using CriaCerto.Modules.Nutrition.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CriaCerto.Modules.Nutrition.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddNutritionInfrastructure(this IServiceCollection services)
    {
        services.AddDbContext<NutritionDbContext>((sp, options) =>
        {
            var connectionProvider = sp.GetRequiredService<ITenantConnectionProvider>();
            options.UseSqlServer(connectionProvider.GetConnectionString(), sqlServerOptions =>
            {
                sqlServerOptions.ConfigureModuleMigrations<NutritionDbContext>("nutrition");
            });

            options.EnableDetailedErrors();
        });

        services.AddScoped<INutritionDbContext>(sp => sp.GetRequiredService<NutritionDbContext>());
        return services;
    }
}
