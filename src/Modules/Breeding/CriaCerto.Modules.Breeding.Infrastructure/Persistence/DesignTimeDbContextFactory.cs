using CriaCerto.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CriaCerto.Modules.Breeding.Infrastructure.Persistence;

public sealed class BreedingDbContextFactory : IDesignTimeDbContextFactory<BreedingDbContext>
{
    public BreedingDbContext CreateDbContext(string[] args)
    {
        var connectionString = DesignTimeConnectionStringResolver.Resolve();

        var optionsBuilder = new DbContextOptionsBuilder<BreedingDbContext>();
        optionsBuilder.UseSqlServer(connectionString, sql =>
            sql.ConfigureModuleMigrations<BreedingDbContext>("breeding"));

        return new BreedingDbContext(optionsBuilder.Options);
    }
}
