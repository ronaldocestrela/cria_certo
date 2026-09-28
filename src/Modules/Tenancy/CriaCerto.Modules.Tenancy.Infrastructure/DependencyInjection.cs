using CriaCerto.BuildingBlocks.Infrastructure.Persistence;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Infrastructure.Persistence;
using CriaCerto.Modules.Tenancy.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CriaCerto.Modules.Tenancy.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTenancyInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("SqlServer")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? "Server=localhost,1433;Database=criacerto_foundation;User Id=sa;Password=Password123!;TrustServerCertificate=True;Encrypt=False";

        services.AddDbContextPool<TenancyDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sqlServerOptions =>
            {
                sqlServerOptions.ConfigureModuleMigrations<TenancyDbContext>("tenancy");
            });
        });

        // Register both interface and DbContext
        services.AddScoped<ITenancyDbContext>(sp => sp.GetRequiredService<TenancyDbContext>());
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<ITenantAccessGuard, TenantAccessGuard>();

        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
        var stripeSection = configuration.GetSection(StripeOptions.SectionName);
        var returnUrl = stripeSection["ReturnUrl"] ?? configuration["STRIPE_RETURN_URL"];

        string? defaultOrigin = null;
        if (!string.IsNullOrWhiteSpace(returnUrl) && Uri.TryCreate(returnUrl, UriKind.Absolute, out var returnUri))
        {
            defaultOrigin = returnUri.GetLeftPart(UriPartial.Authority);
        }

        services.AddSingleton<ISubscriptionUrlValidator>(new CriaCerto.Modules.Tenancy.Application.Services.SubscriptionUrlValidator(allowedOrigins, defaultOrigin));

        services.AddOptions<StripeOptions>()
            .Configure(options =>
            {
                configuration.GetSection(StripeOptions.SectionName).Bind(options);

                var apiKey = configuration["STRIPE_API_KEY"] ?? Environment.GetEnvironmentVariable("STRIPE_API_KEY");
                if (!string.IsNullOrWhiteSpace(apiKey)) options.ApiKey = apiKey;

                var pubKey = configuration["STRIPE_PUBLISHABLE_KEY"] ?? Environment.GetEnvironmentVariable("STRIPE_PUBLISHABLE_KEY");
                if (!string.IsNullOrWhiteSpace(pubKey)) options.PublishableKey = pubKey;

                var webhookSecret = configuration["STRIPE_WEBHOOK_SECRET"] ?? Environment.GetEnvironmentVariable("STRIPE_WEBHOOK_SECRET");
                if (!string.IsNullOrWhiteSpace(webhookSecret)) options.WebhookSecret = webhookSecret;

                var successUrl = configuration["STRIPE_SUCCESS_URL"] ?? Environment.GetEnvironmentVariable("STRIPE_SUCCESS_URL");
                if (!string.IsNullOrWhiteSpace(successUrl)) options.SuccessUrl = successUrl;

                var cancelUrl = configuration["STRIPE_CANCEL_URL"] ?? Environment.GetEnvironmentVariable("STRIPE_CANCEL_URL");
                if (!string.IsNullOrWhiteSpace(cancelUrl)) options.CancelUrl = cancelUrl;

                var returnUrl = configuration["STRIPE_RETURN_URL"] ?? Environment.GetEnvironmentVariable("STRIPE_RETURN_URL");
                if (!string.IsNullOrWhiteSpace(returnUrl)) options.ReturnUrl = returnUrl;
            })
            .Validate(options =>
            {
                var env = configuration["ASPNETCORE_ENVIRONMENT"] ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
                if (string.Equals(env, "Production", StringComparison.OrdinalIgnoreCase))
                {
                    return !string.IsNullOrWhiteSpace(options.WebhookSecret);
                }
                return true;
            }, "A configuração STRIPE_WEBHOOK_SECRET é obrigatória em ambiente de Produção.")
            .ValidateOnStart();

        services.AddScoped<IStripePaymentService, StripePaymentService>();

        services.AddOptions<CriaCerto.Modules.Tenancy.Application.Options.SubscriptionLifecycleOptions>()
            .Bind(configuration.GetSection(CriaCerto.Modules.Tenancy.Application.Options.SubscriptionLifecycleOptions.SectionName));

        services.AddScoped<ISubscriptionLifecycleService, SubscriptionLifecycleService>();

        return services;
    }
}
