using CriaCerto.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CriaCerto.Modules.Calving.Infrastructure.Persistence;

public sealed class CalvingDbContextFactory : IDesignTimeDbContextFactory<CalvingDbContext>
{
    public CalvingDbContext CreateDbContext(string[] args)
    {
        var connectionString = DesignTimeConnectionStringResolver.Resolve();

        var optionsBuilder = new DbContextOptionsBuilder<CalvingDbContext>();
        optionsBuilder.UseSqlServer(connectionString, sql =>
            sql.ConfigureModuleMigrations<CalvingDbContext>("calving"));

        return new CalvingDbContext(optionsBuilder.Options);
    }
}
