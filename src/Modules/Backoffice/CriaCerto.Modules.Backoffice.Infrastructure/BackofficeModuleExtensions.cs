using CriaCerto.Modules.Backoffice.Infrastructure.Persistence;
using CriaCerto.Modules.Backoffice.Infrastructure.Security;
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

        return services;
    }

    public static IApplicationBuilder UseBackofficeModule(this IApplicationBuilder app)
    {
        app.UseMiddleware<BackofficeAccessMiddleware>();
        return app;
    }
}
