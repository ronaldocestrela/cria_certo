using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CriaCerto.BuildingBlocks.Infrastructure.Persistence;

public static class DatabaseMigrationRunner
{
    public static void ApplyMigrations(DbContext dbContext, ILogger logger)
    {
        var dbContextName = dbContext.GetType().Name;

        if (dbContext.Database.IsRelational())
        {
            ExistingDatabaseBaseline.ApplyIfNeeded(dbContext, logger);
            logger.LogInformation("Applying migrations for {DbContextName}...", dbContextName);
            dbContext.Database.Migrate();
            logger.LogInformation("Migrations applied for {DbContextName}.", dbContextName);
            return;
        }

        logger.LogInformation("Ensuring database created for non-relational {DbContextName}...", dbContextName);
        dbContext.Database.EnsureCreated();
        logger.LogInformation("EnsureCreated completed for {DbContextName}.", dbContextName);
    }

    public static async Task ApplyMigrationsAsync(
        DbContext dbContext,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var dbContextName = dbContext.GetType().Name;

        if (dbContext.Database.IsRelational())
        {
            await ExistingDatabaseBaseline.ApplyIfNeededAsync(dbContext, logger, cancellationToken);
            logger.LogInformation("Applying migrations for {DbContextName}...", dbContextName);
            await dbContext.Database.MigrateAsync(cancellationToken);
            logger.LogInformation("Migrations applied for {DbContextName}.", dbContextName);
            return;
        }

        logger.LogInformation("Ensuring database created for non-relational {DbContextName}...", dbContextName);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        logger.LogInformation("EnsureCreated completed for {DbContextName}.", dbContextName);
    }
}
