using CriaCerto.BuildingBlocks.Abstractions.Tenancy;
using CriaCerto.BuildingBlocks.Application.Abstractions;
using CriaCerto.BuildingBlocks.Infrastructure.Persistence;
using CriaCerto.BuildingBlocks.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CriaCerto.BuildingBlocks.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBuildingBlocksInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ITenantContext, HttpContextTenantContext>();
        services.AddScoped<ITenantConnectionProvider, TenantConnectionProvider>();
        services.AddSingleton<ITenantDatabaseProvisioner, TenantDatabaseProvisioner>();

        services.AddDbContextPool<FoundationDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sqlServerOptions =>
            {
                sqlServerOptions.ConfigureModuleMigrations<FoundationDbContext>("foundation");
            });

            options.EnableDetailedErrors();
        });

        services.AddScoped<IFoundationDbContext>(sp => sp.GetRequiredService<FoundationDbContext>());

        return services;
    }

    public static IServiceCollection AddEmailSender(
        this IServiceCollection services,
        Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        services.Configure<CriaCerto.BuildingBlocks.Infrastructure.Email.ResendOptions>(options =>
        {
            configuration.GetSection(CriaCerto.BuildingBlocks.Infrastructure.Email.ResendOptions.SectionName).Bind(options);
            
            // Allow env variable overrides
            var envApiKey = configuration["RESEND_API_KEY"];
            if (!string.IsNullOrWhiteSpace(envApiKey))
            {
                options.ApiKey = envApiKey;
            }

            var envFromEmail = configuration["RESEND_FROM_EMAIL"];
            if (!string.IsNullOrWhiteSpace(envFromEmail))
            {
                options.DefaultFromEmail = envFromEmail;
            }

            var envFromName = configuration["RESEND_FROM_NAME"];
            if (!string.IsNullOrWhiteSpace(envFromName))
            {
                options.DefaultFromName = envFromName;
            }
        });

        var resendSection = configuration.GetSection(CriaCerto.BuildingBlocks.Infrastructure.Email.ResendOptions.SectionName);
        var apiKey = configuration["RESEND_API_KEY"] ?? resendSection["ApiKey"];

        var envMockStr = configuration["RESEND_ENABLE_MOCK_DEV"];
        bool enableMock;
        if (!string.IsNullOrWhiteSpace(envMockStr) && bool.TryParse(envMockStr, out var parsedMock))
        {
            enableMock = parsedMock;
        }
        else
        {
            enableMock = string.IsNullOrWhiteSpace(apiKey) || resendSection.GetValue<bool>("EnableMockInDevelopment");
        }

        if (enableMock || string.IsNullOrWhiteSpace(apiKey))
        {
            services.AddSingleton<CriaCerto.BuildingBlocks.Abstractions.Email.IEmailSender, CriaCerto.BuildingBlocks.Infrastructure.Email.NullEmailSender>();
        }
        else
        {
            services.AddHttpClient<CriaCerto.BuildingBlocks.Abstractions.Email.IEmailSender, CriaCerto.BuildingBlocks.Infrastructure.Email.ResendEmailSender>(client =>
            {
                var baseUrl = resendSection["BaseUrl"] ?? "https://api.resend.com";
                client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
                client.Timeout = TimeSpan.FromSeconds(15);
            });
        }

        return services;
    }

    public static IServiceCollection AddDataEncryption(
        this IServiceCollection services,
        Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        services.Configure<CriaCerto.BuildingBlocks.Abstractions.Security.EncryptionOptions>(options =>
        {
            configuration.GetSection(CriaCerto.BuildingBlocks.Abstractions.Security.EncryptionOptions.SectionName).Bind(options);

            var envKey = configuration["ENCRYPTION_CURRENT_KEY"];
            if (!string.IsNullOrWhiteSpace(envKey))
            {
                options.CurrentKey = envKey;
            }

            var envBlindKey = configuration["ENCRYPTION_BLIND_INDEX_KEY"];
            if (!string.IsNullOrWhiteSpace(envBlindKey))
            {
                options.BlindIndexKey = envBlindKey;
            }
        });

        services.AddSingleton<CriaCerto.BuildingBlocks.Abstractions.Security.IDataEncryptionService, CriaCerto.BuildingBlocks.Infrastructure.Security.AesGcmDataEncryptionService>();
        return services;
    }
}