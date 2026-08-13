using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CriaCerto.BuildingBlocks.Infrastructure.Persistence;

public static class SqlServerMigrationExtensions
{
    public const string HistoryTableName = "__EFMigrationsHistory";

    public static void ConfigureModuleMigrations<TContext>(
        this SqlServerDbContextOptionsBuilder options,
        string historySchema)
        where TContext : DbContext
    {
        options.MigrationsAssembly(typeof(TContext).Assembly.FullName);
        options.MigrationsHistoryTable(HistoryTableName, historySchema);
        options.EnableRetryOnFailure(maxRetryCount: 3);
    }
}
