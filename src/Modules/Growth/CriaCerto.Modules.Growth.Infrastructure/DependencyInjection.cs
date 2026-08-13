using CriaCerto.BuildingBlocks.Abstractions.Tenancy;
using CriaCerto.BuildingBlocks.Infrastructure.Persistence;
using CriaCerto.Modules.Growth.Application.Abstractions;
using CriaCerto.Modules.Growth.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CriaCerto.Modules.Growth.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddGrowthInfrastructure(this IServiceCollection services)
    {
        services.AddDbContext<GrowthDbContext>((sp, options) =>
        {
            var connectionProvider = sp.GetRequiredService<ITenantConnectionProvider>();
            options.UseSqlServer(connectionProvider.GetConnectionString(), sqlServerOptions =>
            {
                sqlServerOptions.ConfigureModuleMigrations<GrowthDbContext>("growth");
            });

            options.EnableDetailedErrors();
        });

        services.AddScoped<IGrowthDbContext>(sp => sp.GetRequiredService<GrowthDbContext>());

        // Scale file parsers DI
        services.AddTransient<CriaCerto.Modules.Growth.Application.Services.ScaleParsers.IWeighingScaleFileParser, CriaCerto.Modules.Growth.Application.Services.ScaleParsers.TruTestScaleParser>();
        services.AddTransient<CriaCerto.Modules.Growth.Application.Services.ScaleParsers.IWeighingScaleFileParser, CriaCerto.Modules.Growth.Application.Services.ScaleParsers.CoimmaScaleParser>();
        services.AddTransient<CriaCerto.Modules.Growth.Application.Services.ScaleParsers.IWeighingScaleFileParser, CriaCerto.Modules.Growth.Application.Services.ScaleParsers.ToledoScaleParser>();
        services.AddTransient<CriaCerto.Modules.Growth.Application.Services.ScaleParsers.IWeighingScaleFileParser, CriaCerto.Modules.Growth.Application.Services.ScaleParsers.GenericCsvScaleParser>();
        services.AddTransient<CriaCerto.Modules.Growth.Application.Services.ScaleParsers.IScaleFileParserFactory, CriaCerto.Modules.Growth.Application.Services.ScaleParsers.ScaleFileParserFactory>();

        return services;
    }
}
