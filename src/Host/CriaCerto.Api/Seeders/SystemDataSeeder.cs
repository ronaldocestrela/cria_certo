using CriaCerto.BuildingBlocks.Infrastructure.Persistence;
using CriaCerto.BuildingBlocks.Infrastructure.Persistence.Seeders;
using CriaCerto.Modules.Backoffice.Infrastructure.Persistence;
using CriaCerto.Modules.Backoffice.Infrastructure.Persistence.Seeders;
using CriaCerto.Modules.Backoffice.Infrastructure.Security;
using CriaCerto.Modules.Sanitary.Infrastructure.Persistence;
using CriaCerto.Modules.Sanitary.Infrastructure.Persistence.Seeders;

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

            await SeedDataAsync(foundationDb, sanitaryDb, backofficeDb, passwordHasher, cancellationToken);
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
        CancellationToken cancellationToken = default)
    {
        await BovineBreedSeeder.SeedAsync(foundationDb, cancellationToken);
        await VaccineReferenceSeeder.SeedAsync(sanitaryDb, cancellationToken);
        await BackofficeDataSeeder.SeedAsync(backofficeDb, passwordHasher, cancellationToken);
    }
}

