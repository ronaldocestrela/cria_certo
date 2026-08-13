using CriaCerto.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CriaCerto.Modules.Backoffice.Infrastructure.Persistence;

public sealed class BackofficeDbContextFactory : IDesignTimeDbContextFactory<BackofficeDbContext>
{
    public BackofficeDbContext CreateDbContext(string[] args)
    {
        var connectionString = DesignTimeConnectionStringResolver.Resolve();

        var optionsBuilder = new DbContextOptionsBuilder<BackofficeDbContext>();
        optionsBuilder.UseSqlServer(connectionString, sql =>
            sql.ConfigureModuleMigrations<BackofficeDbContext>("backoffice"));

        return new BackofficeDbContext(optionsBuilder.Options);
    }
}
