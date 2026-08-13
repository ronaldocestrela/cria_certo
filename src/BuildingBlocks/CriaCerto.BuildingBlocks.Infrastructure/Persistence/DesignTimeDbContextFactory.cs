using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace CriaCerto.BuildingBlocks.Infrastructure.Persistence;

public sealed class FoundationDbContextFactory : IDesignTimeDbContextFactory<FoundationDbContext>
{
    public FoundationDbContext CreateDbContext(string[] args)
    {
        var connectionString = DesignTimeConnectionStringResolver.Resolve();

        var optionsBuilder = new DbContextOptionsBuilder<FoundationDbContext>();
        optionsBuilder.UseSqlServer(connectionString, sql =>
            sql.ConfigureModuleMigrations<FoundationDbContext>("foundation"));

        return new FoundationDbContext(optionsBuilder.Options);
    }
}

public static class DesignTimeConnectionStringResolver
{
    public static string Resolve()
    {
        var basePath = Directory.GetCurrentDirectory();
        var apiPath = Path.Combine(basePath, "src", "Host", "CriaCerto.Api");
        if (!Directory.Exists(apiPath))
        {
            apiPath = Path.GetFullPath(Path.Combine(basePath, "..", "..", "..", "Host", "CriaCerto.Api"));
        }

        if (Directory.Exists(apiPath))
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(apiPath)
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var configured = configuration.GetConnectionString("SqlServer")
                ?? configuration.GetConnectionString("DefaultConnection");

            if (!string.IsNullOrWhiteSpace(configured))
            {
                return configured;
            }
        }

        return "Server=localhost,1433;Database=criacerto_foundation;User Id=sa;Password=Password123!;TrustServerCertificate=True;Encrypt=False";
    }
}
