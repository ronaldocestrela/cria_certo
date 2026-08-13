using CriaCerto.BuildingBlocks.Abstractions.Tenancy;
using CriaCerto.BuildingBlocks.Infrastructure.Persistence;
using CriaCerto.Modules.Calving.Application.Abstractions;
using CriaCerto.Modules.Calving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CriaCerto.Modules.Calving.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCalvingInfrastructure(this IServiceCollection services)
    {
        services.AddDbContext<CalvingDbContext>((sp, options) =>
        {
            var connectionProvider = sp.GetRequiredService<ITenantConnectionProvider>();
            options.UseSqlServer(connectionProvider.GetConnectionString(), sqlServerOptions =>
            {
                sqlServerOptions.ConfigureModuleMigrations<CalvingDbContext>("calving");
            });

            options.EnableDetailedErrors();
        });

        services.AddScoped<ICalvingDbContext>(sp => sp.GetRequiredService<CalvingDbContext>());
        return services;
    }
}
