using CriaCerto.Api.Seeders;
using CriaCerto.BuildingBlocks.Infrastructure.Persistence;
using CriaCerto.Modules.Backoffice.Infrastructure.Persistence;
using CriaCerto.Modules.Breeding.Infrastructure.Persistence;
using CriaCerto.Modules.Calving.Infrastructure.Persistence;
using CriaCerto.Modules.Growth.Infrastructure.Persistence;
using CriaCerto.Modules.Nutrition.Infrastructure.Persistence;
using CriaCerto.Modules.Referral.Infrastructure.Persistence;
using CriaCerto.Modules.Sanitary.Infrastructure.Persistence;
using CriaCerto.Modules.Tenancy.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CriaCerto.Api.Configuration;

public static class DatabaseMigrationExtensions
{
    public static WebApplication ApplyDatabaseMigrations(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Startup.Migrations");

        var dbContexts = new DbContext[]
        {
            scope.ServiceProvider.GetRequiredService<FoundationDbContext>(),
            scope.ServiceProvider.GetRequiredService<TenancyDbContext>(),
            scope.ServiceProvider.GetRequiredService<BreedingDbContext>(),
            scope.ServiceProvider.GetRequiredService<CalvingDbContext>(),
            scope.ServiceProvider.GetRequiredService<GrowthDbContext>(),
            scope.ServiceProvider.GetRequiredService<NutritionDbContext>(),
            scope.ServiceProvider.GetRequiredService<SanitaryDbContext>(),
            scope.ServiceProvider.GetRequiredService<BackofficeDbContext>(),
            scope.ServiceProvider.GetRequiredService<ReferralDbContext>()
        };

        foreach (var dbContext in dbContexts)
        {
            DatabaseMigrationRunner.ApplyMigrations(dbContext, logger);
        }

        return app;
    }

    public static WebApplication SeedReferenceDatabase(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        SystemDataSeeder.SeedAsync(scope.ServiceProvider).GetAwaiter().GetResult();
        return app;
    }
}

