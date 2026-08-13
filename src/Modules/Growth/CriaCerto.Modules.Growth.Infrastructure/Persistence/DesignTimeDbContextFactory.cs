using CriaCerto.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CriaCerto.Modules.Growth.Infrastructure.Persistence;

public sealed class GrowthDbContextFactory : IDesignTimeDbContextFactory<GrowthDbContext>
{
    public GrowthDbContext CreateDbContext(string[] args)
    {
        var connectionString = DesignTimeConnectionStringResolver.Resolve();

        var optionsBuilder = new DbContextOptionsBuilder<GrowthDbContext>();
        optionsBuilder.UseSqlServer(connectionString, sql =>
            sql.ConfigureModuleMigrations<GrowthDbContext>("growth"));

        return new GrowthDbContext(optionsBuilder.Options);
    }
}
