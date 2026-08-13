using CriaCerto.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CriaCerto.Modules.Sanitary.Infrastructure.Persistence;

public sealed class SanitaryDbContextFactory : IDesignTimeDbContextFactory<SanitaryDbContext>
{
    public SanitaryDbContext CreateDbContext(string[] args)
    {
        var connectionString = DesignTimeConnectionStringResolver.Resolve();

        var optionsBuilder = new DbContextOptionsBuilder<SanitaryDbContext>();
        optionsBuilder.UseSqlServer(connectionString, sql =>
            sql.ConfigureModuleMigrations<SanitaryDbContext>("sanitary"));

        return new SanitaryDbContext(optionsBuilder.Options);
    }
}
