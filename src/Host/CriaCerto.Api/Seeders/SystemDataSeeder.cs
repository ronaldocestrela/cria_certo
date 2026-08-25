using CriaCerto.BuildingBlocks.Infrastructure.Persistence;
using CriaCerto.BuildingBlocks.Infrastructure.Persistence.Seeders;
using CriaCerto.Modules.Backoffice.Infrastructure.Persistence;
using CriaCerto.Modules.Backoffice.Infrastructure.Persistence.Seeders;
using CriaCerto.Modules.Backoffice.Application.Security;
using CriaCerto.Modules.Sanitary.Infrastructure.Persistence;
using CriaCerto.Modules.Sanitary.Infrastructure.Persistence.Seeders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CriaCerto.Api.Seeders;

public static class SystemDataSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var logger = scope.ServiceProvider.GetService<ILoggerFactory>()?.CreateLogger("SystemDataSeeder");

        try
        {
            var foundationDb = scope.ServiceProvider.GetRequiredService<FoundationDbContext>();
            var sanitaryDb = scope.ServiceProvider.GetRequiredService<SanitaryDbContext>();
            var backofficeDb = scope.ServiceProvider.GetRequiredService<BackofficeDbContext>();
            var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasherService>();
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var resetBootstrapAdminPassword = configuration.GetValue<bool>("Backoffice:ResetBootstrapAdminPassword");

            await SeedDataAsync(
                foundationDb,
                sanitaryDb,
                backofficeDb,
                passwordHasher,
                logger,
                resetBootstrapAdminPassword,
                cancellationToken);
            logger?.LogInformation("[SystemDataSeeder] Reference and backoffice data seeded successfully.");
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "[SystemDataSeeder] Error seeding reference data.");
            throw;
        }
    }

    public static async Task SeedDataAsync(
        FoundationDbContext foundationDb,
        SanitaryDbContext sanitaryDb,
        BackofficeDbContext backofficeDb,
        IPasswordHasherService passwordHasher,
        ILogger? logger = null,
        bool resetBootstrapAdminPassword = false,
        CancellationToken cancellationToken = default)
    {
        logger?.LogInformation("[SystemDataSeeder] Seeding bovine breeds...");
        await BovineBreedSeeder.SeedAsync(foundationDb, cancellationToken);
        logger?.LogInformation("[SystemDataSeeder] Bovine breeds seeded.");

        logger?.LogInformation("[SystemDataSeeder] Seeding vaccine references...");
        await VaccineReferenceSeeder.SeedAsync(sanitaryDb, cancellationToken);
        logger?.LogInformation("[SystemDataSeeder] Vaccine references seeded.");

        logger?.LogInformation("[SystemDataSeeder] Seeding backoffice IAM data...");
        await BackofficeDataSeeder.SeedAsync(
            backofficeDb,
            passwordHasher,
            logger,
            resetBootstrapAdminPassword,
            cancellationToken);
        logger?.LogInformation("[SystemDataSeeder] Backoffice IAM data seeded.");
    }
}
