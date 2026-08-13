using CriaCerto.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CriaCerto.Modules.Nutrition.Infrastructure.Persistence;

public sealed class NutritionDbContextFactory : IDesignTimeDbContextFactory<NutritionDbContext>
{
    public NutritionDbContext CreateDbContext(string[] args)
    {
        var connectionString = DesignTimeConnectionStringResolver.Resolve();

        var optionsBuilder = new DbContextOptionsBuilder<NutritionDbContext>();
        optionsBuilder.UseSqlServer(connectionString, sql =>
            sql.ConfigureModuleMigrations<NutritionDbContext>("nutrition"));

        return new NutritionDbContext(optionsBuilder.Options);
    }
}
