using CriaCerto.BuildingBlocks.Infrastructure.Persistence;
using CriaCerto.Modules.Referral.Application.Abstractions;
using CriaCerto.Modules.Referral.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CriaCerto.Modules.Referral.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddReferralInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("SqlServer")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? "Server=localhost,1433;Database=criacerto_foundation;User Id=sa;Password=Password123!;TrustServerCertificate=True;Encrypt=False";

        services.AddDbContext<ReferralDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sql =>
                sql.ConfigureModuleMigrations<ReferralDbContext>("referral"));
        });

        services.AddScoped<IReferralDbContext>(sp => sp.GetRequiredService<ReferralDbContext>());

        var receiptsDir = configuration["Storage:PayoutReceiptsDirectory"];
        services.AddSingleton<IPayoutReceiptStorageService>(new Storage.LocalPayoutReceiptStorageService(receiptsDir));
        services.AddSingleton<Application.Features.Payouts.Services.IPixBatchCsvGenerator, Application.Features.Payouts.Services.PixBatchCsvGenerator>();
        services.AddScoped<IReferralMaturityEnforcementService, Application.Features.Enforcement.Services.ReferralMaturityEnforcementService>();
        services.AddScoped<IReferralDataReencryptionService, Services.ReferralDataReencryptionService>();

        return services;
    }
}
