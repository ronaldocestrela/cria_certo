using CriaCerto.BuildingBlocks.Infrastructure.Persistence;
using CriaCerto.Modules.Backoffice.Application.Security;
using CriaCerto.Modules.Backoffice.Infrastructure.Persistence;
using CriaCerto.Modules.Backoffice.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using DbContext = Microsoft.EntityFrameworkCore.DbContext;

namespace CriaCerto.Modules.Backoffice.Infrastructure;

public static class BackofficeModuleExtensions
{
    public static IServiceCollection AddBackofficeInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("SqlServer")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? "Server=localhost,1433;Database=criacerto_foundation;User Id=sa;Password=Password123!;TrustServerCertificate=True;Encrypt=False";

        services.AddDbContext<BackofficeDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sql =>
                sql.ConfigureModuleMigrations<BackofficeDbContext>("backoffice"));
        });

        services.AddScoped<DbContext>(sp => sp.GetRequiredService<BackofficeDbContext>());

        // Register Granular RBAC & Policy Authorization Services
        services.AddScoped<IPermissionEvaluator, PermissionEvaluatorService>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddSingleton<IAuthorizationPolicyProvider, BackofficePermissionPolicyProvider>();

        // Register Security & IAM Services
        services.AddSingleton<IPasswordHasherService, PasswordHasherService>();
        services.AddSingleton<ITotpService, TotpService>();
        services.AddSingleton<IBackofficeTokenService, BackofficeTokenService>();

        // Register Observability & Anomaly Services
        services.AddScoped<CriaCerto.Modules.Backoffice.Application.Features.Observability.Services.IAnomalyDetectionEngine, CriaCerto.Modules.Backoffice.Application.Features.Observability.Services.AnomalyDetectionEngine>();
        services.AddTransient(typeof(MediatR.IPipelineBehavior<,>), typeof(CriaCerto.Modules.Backoffice.Application.Telemetry.BackofficeObservabilityBehavior<,>));

        return services;
    }

    public static IApplicationBuilder UseBackofficeModule(this IApplicationBuilder app)
    {
        app.UseMiddleware<BackofficeAccessMiddleware>();
        return app;
    }
}
