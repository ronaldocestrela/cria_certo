using CriaCerto.Modules.Backoffice.Application.Security;
using CriaCerto.Modules.Backoffice.Infrastructure.Persistence;
using CriaCerto.Modules.Backoffice.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
            options.UseSqlServer(connectionString, b => b.MigrationsAssembly(typeof(BackofficeDbContext).Assembly.FullName));
        });

        // Register Granular RBAC & Policy Authorization Services
        services.AddScoped<IPermissionEvaluator, PermissionEvaluatorService>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddSingleton<IAuthorizationPolicyProvider, BackofficePermissionPolicyProvider>();

        // Register Security & IAM Services
        services.AddSingleton<IPasswordHasherService, PasswordHasherService>();
        services.AddSingleton<ITotpService, TotpService>();

        return services;
    }

    public static IApplicationBuilder UseBackofficeModule(this IApplicationBuilder app)
    {
        app.UseMiddleware<BackofficeAccessMiddleware>();
        return app;
    }
}
