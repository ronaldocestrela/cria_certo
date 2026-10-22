using CriaCerto.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CriaCerto.Modules.Referral.Infrastructure.Persistence;

public sealed class ReferralDbContextFactory : IDesignTimeDbContextFactory<ReferralDbContext>
{
    public ReferralDbContext CreateDbContext(string[] args)
    {
        var connectionString = DesignTimeConnectionStringResolver.Resolve();

        var optionsBuilder = new DbContextOptionsBuilder<ReferralDbContext>();
        optionsBuilder.UseSqlServer(connectionString, sql =>
            sql.ConfigureModuleMigrations<ReferralDbContext>("referral"));

        return new ReferralDbContext(optionsBuilder.Options);
    }
}
