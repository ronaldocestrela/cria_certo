using CriaCerto.Shared.UI.Auth;
using CriaCerto.Shared.UI.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace CriaCerto.Shared.UI.Services;

public static class CriaCertoSharedServiceCollectionExtensions
{
    public static IServiceCollection AddCriaCertoSharedServices(this IServiceCollection services)
    {
        services.AddAuthorizationCore();
        services.AddCascadingAuthenticationState();

        services.AddScoped<CustomAuthStateProvider>();
        services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<CustomAuthStateProvider>());

        // Api Clients
        services.AddScoped<PlantelApiClient>();
        services.AddScoped<BreedingOpsApiClient>();
        services.AddScoped<GrowthApiClient>();
        services.AddScoped<NutritionApiClient>();
        services.AddScoped<CalvingApiClient>();
        services.AddScoped<SanitaryApiClient>();
        services.AddScoped<AnalyticsApiClient>();
        services.AddScoped<TenancyApiClient>();
        services.AddScoped<BackofficeApiClient>();
        services.AddScoped<IBackofficeApiClient>(sp => sp.GetRequiredService<BackofficeApiClient>());
        services.AddScoped<IBackofficePermissionService, BackofficePermissionService>();
        services.AddScoped<IImpersonationStateService, ImpersonationStateService>();
        services.AddScoped<IOfflineSyncService, OfflineSyncService>();
        services.AddScoped<IToastService, ToastService>();
        services.AddScoped<IFeatureFlagClientService, FeatureFlagClientService>();
        services.AddScoped<IReferralTrackingService, ReferralTrackingService>();
        services.AddScoped<ReferralApiClient>();
        services.AddScoped<IReferralApiClient>(sp => sp.GetRequiredService<ReferralApiClient>());
        services.AddScoped<PartnerApiClient>();
        services.AddScoped<IPartnerApiClient>(sp => sp.GetRequiredService<PartnerApiClient>());
        services.AddScoped<ICookieConsentService, CookieConsentService>();

        return services;
    }
}

