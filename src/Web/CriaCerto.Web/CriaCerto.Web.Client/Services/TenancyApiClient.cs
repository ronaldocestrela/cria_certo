using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.JSInterop;

namespace CriaCerto.Web.Client.Services;

public sealed record SubscriptionPlanFeatureModel(
    string Key,
    string Name,
    bool IsEnabled = true
);

public sealed record SubscriptionPlanModel(
    string PlanId,
    string Name,
    string Description,
    decimal MonthlyPrice,
    decimal AnnualPriceMonthly,
    int HeadCapacityLimit,
    IReadOnlyList<string> IncludedModules,
    bool IsPopular,
    IReadOnlyList<SubscriptionPlanFeatureModel>? Features = null
);

public sealed record TenantProfileModel(
    Guid Id,
    string Name,
    string CNPJ,
    string Status,
    string SubscribedPlan,
    int Capacity,
    string State,
    string City,
    string StateRegistration,
    decimal AreaInHectares,
    string Type,
    string? StripeCustomerId = null,
    string? StripeSubscriptionId = null,
    DateTime? CurrentPeriodEndUtc = null,
    bool CancelAtPeriodEnd = false,
    string HeadTierCode = "TIER_500",
    IReadOnlyList<string>? SubscribedModules = null
);

public sealed record HeadTierOptionModel(
    string TierCode,
    string DisplayName,
    int MinHeads,
    int MaxHeads,
    int SortOrder
);

public sealed record ModuleTierPriceOptionModel(
    string TierCode,
    decimal MonthlyPrice,
    decimal AnnualPriceMonthly,
    string? StripePriceIdMonthly = null,
    string? StripePriceIdAnnual = null
);

public sealed record CommercialModuleOptionModel(
    string ModuleKey,
    string Name,
    string Description,
    string Icon,
    bool IsCore,
    int SortOrder,
    IReadOnlyList<ModuleTierPriceOptionModel> PricesByTier
)
{
    public string IconName => ResolveIcon(Icon, ModuleKey);

    public static string ResolveIcon(string? icon, string? moduleKey = null)
    {
        if (!string.IsNullOrWhiteSpace(icon) && !icon.StartsWith("bi-", StringComparison.OrdinalIgnoreCase) && !icon.StartsWith("bi ", StringComparison.OrdinalIgnoreCase))
        {
            return icon.Trim();
        }

        var normalizedIcon = icon?.Trim().ToLowerInvariant() ?? string.Empty;
        var key = moduleKey?.Trim().ToLowerInvariant() ?? string.Empty;

        return normalizedIcon switch
        {
            "bi-grid-1x2-fill" or "bi-grid" or "bi-cow" => "pets",
            "bi-heart-pulse-fill" or "bi-heart-pulse" => "favorite",
            "bi-stars" or "bi-star-fill" => "child_care",
            "bi-graph-up-arrow" or "bi-graph-up" => "trending_up",
            "bi-shield-fill-plus" or "bi-shield-plus" => "vaccines",
            "bi-basket2-fill" or "bi-basket" => "restaurant",
            "bi-bar-chart-line-fill" or "bi-bar-chart" => "insights",
            _ => key switch
            {
                "cows" => "pets",
                "breeding" => "favorite",
                "calving" => "child_care",
                "growth" => "trending_up",
                "sanitary" => "vaccines",
                "nutrition" => "restaurant",
                "analytics" => "insights",
                _ => "widgets"
            }
        };
    }
}

public sealed record ModularSubscriptionCatalogModel(
    IReadOnlyList<HeadTierOptionModel> HeadTiers,
    IReadOnlyList<CommercialModuleOptionModel> Modules
);

public sealed record UpdateModularSubscriptionRequestModel(
    Guid TenantId,
    string? HeadTierCode = null,
    List<string>? SelectedModules = null,
    string? BillingCycle = "monthly",
    bool AllowGracePeriod = false
);

public sealed record UpdateModularSubscriptionResultModel(
    Guid TenantId,
    string HeadTierCode,
    int HeadCapacity,
    IReadOnlyList<string> SubscribedModules,
    string Status,
    DateTime? GracePeriodEndsAtUtc,
    string? PendingHeadTierCode,
    string Token,
    TenantProfileModel Profile
);

public sealed record StripeCheckoutSessionResponse(string SessionId, string Url);
public sealed record StripePortalSessionResponse(string Url);
public sealed record RefreshTokenResponseDto(string Token, Guid TenantId, string SubscribedPlan, string Role);
public sealed record ApiErrorDto(string? Code, string? Message, int? Type);

public sealed record SubmitDataSubjectRequestDto(
    string FullName,
    string Email,
    string? PhoneNumber,
    string? DocumentNumber,
    int RelationshipType,
    int RequestType,
    string Description
);

public sealed record DataSubjectRequestReceiptModel(
    string ProtocolNumber,
    string DataSubjectName,
    string DataSubjectEmail,
    int RelationshipType,
    int RequestType,
    DateTime SubmittedAtUtc,
    DateTime MaxResponseDateUtc,
    int EstimatedBusinessDays,
    string DpoEmail,
    string ConfirmationMessage
);

public sealed record ProductionUnitModel(
    Guid Id,
    Guid TenantId,
    string Code,
    string Name,
    string Type,
    string Status,
    int Capacity,
    int CurrentHeadCount,
    string? LocationDetails,
    decimal OccupancyPercentage
);

public sealed record UpdateTenantProfileRequest(
    Guid TenantId,
    string Name,
    string CNPJ,
    string State,
    string City,
    string StateRegistration,
    decimal AreaInHectares,
    int Capacity,
    string Type
);

public sealed record CreateProductionUnitRequest(
    Guid TenantId,
    string Name,
    string Type,
    int Capacity,
    string? LocationDetails
);

public sealed record TeamMemberModel(
    Guid UserId,
    string Email,
    string FullName,
    int Role,
    DateTime JoinedAt,
    bool IsActive
);

public sealed record TeamInviteModel(
    Guid Id,
    Guid TenantId,
    string Email,
    int Role,
    string InviteToken,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    bool IsAccepted
);

public sealed record TeamOverviewModel(
    List<TeamMemberModel> Members,
    List<TeamInviteModel> PendingInvites
);

public sealed record InviteTeamMemberRequest(
    Guid TenantId,
    string Email,
    int Role
);

public sealed record UserTenantAffiliationModel(
    Guid TenantId,
    string TenantName,
    string Role,
    DateTime JoinedAt,
    string SubscribedPlan
);

public sealed record UserProfileModel(
    Guid Id,
    string Email,
    string FullName,
    string? PhoneNumber,
    bool IsEmailConfirmed,
    DateTime? EmailConfirmedAtUtc,
    DateTime CreatedAtUtc,
    DateTime? TermsAcceptedAtUtc,
    DateTime? PrivacyPolicyAcceptedAtUtc,
    string? TermsVersion,
    string? PrivacyPolicyVersion,
    IReadOnlyList<UserTenantAffiliationModel> AssociatedTenants
);

public sealed record UpdateUserProfileRequest(
    string FullName,
    string? PhoneNumber
);

public sealed record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword,
    string ConfirmNewPassword
);

public sealed record AnonymizeAccountRequest(
    string CurrentPassword,
    string ConfirmationText,
    string? ExitReason = null
);

public sealed class TenancyApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IJSRuntime? _jsRuntime;

    public TenancyApiClient(HttpClient httpClient, IJSRuntime? jsRuntime = null)
    {
        _httpClient = httpClient;
        _jsRuntime = jsRuntime;
    }

    public async Task<List<SubscriptionPlanModel>> GetSubscriptionPlansAsync(CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        try
        {
            var plans = await _httpClient.GetFromJsonAsync<List<SubscriptionPlanModel>>("/api/v1/tenancy/plans", cancellationToken);
            if (plans is { Count: > 0 })
            {
                return plans;
            }
        }
        catch
        {
            // Fallback for offline or client render preview
        }

        return GetFallbackPlans();
    }

    public async Task<ModularSubscriptionCatalogModel> GetModularCatalogAsync(CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        try
        {
            var catalog = await _httpClient.GetFromJsonAsync<ModularSubscriptionCatalogModel>("/api/v1/tenancy/catalog", cancellationToken);
            if (catalog is { HeadTiers.Count: > 0, Modules.Count: > 0 })
            {
                return catalog;
            }
        }
        catch
        {
            // Fallback for offline or client render preview
        }

        return GetDefaultModularCatalog();
    }

    public async Task<TenantProfileModel?> GetTenantProfileAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        try
        {
            return await _httpClient.GetFromJsonAsync<TenantProfileModel>($"/api/v1/tenancy/profile?tenantId={tenantId}", cancellationToken);
        }
        catch
        {
            return null;
        }
    }

        public async Task<string?> RefreshTokenAsync(
            Guid? tenantId = null,
            CancellationToken cancellationToken = default)
        {
            await AttachTokenAsync();
            try
            {
                var payload = new { TenantId = tenantId };
                var response = await _httpClient.PostAsJsonAsync("/api/v1/auth/refresh-token", payload, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<RefreshTokenResponseDto>(cancellationToken: cancellationToken);
                    return result?.Token;
                }
            }
            catch
            {
                // Fallback for network/offline scenario
            }

            return null;
        }

    public async Task<bool> UpdateTenantProfileAsync(UpdateTenantProfileRequest request, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        try
        {
            var response = await _httpClient.PutAsJsonAsync("/api/v1/tenancy/profile", request, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<StripeCheckoutSessionResponse?> CreateSubscriptionCheckoutAsync(
        Guid tenantId,
        string planId,
        string billingCycle = "monthly",
        string? successUrl = null,
        string? cancelUrl = null,
        CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        try
        {
            var payload = new
            {
                tenantId,
                planId,
                billingCycle,
                successUrl,
                cancelUrl
            };
            var response = await _httpClient.PostAsJsonAsync("/api/v1/tenancy/subscription/checkout", payload, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<StripeCheckoutSessionResponse>(cancellationToken: cancellationToken);
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                var errorObj = await response.Content.ReadFromJsonAsync<ApiErrorDto>(cancellationToken: cancellationToken);
                throw new InvalidOperationException(errorObj?.Message ?? "A fazenda já possui uma assinatura ativa no Stripe. Alterações de plano devem ser realizadas com segurança através do portal de gerenciamento de faturamento.");
            }
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch
        {
        }

        return null;
    }

    public async Task<StripeCheckoutSessionResponse?> CreateModularCheckoutAsync(
        Guid tenantId,
        string headTierCode,
        List<string> selectedModules,
        string billingCycle = "monthly",
        string? successUrl = null,
        string? cancelUrl = null,
        CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        try
        {
            var payload = new
            {
                tenantId,
                headTierCode,
                selectedModules,
                billingCycle,
                successUrl,
                cancelUrl
            };
            var response = await _httpClient.PostAsJsonAsync("/api/v1/tenancy/subscription/checkout", payload, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<StripeCheckoutSessionResponse>(cancellationToken: cancellationToken);
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                var errorObj = await response.Content.ReadFromJsonAsync<ApiErrorDto>(cancellationToken: cancellationToken);
                throw new InvalidOperationException(errorObj?.Message ?? "A fazenda já possui uma assinatura ativa no Stripe. Alterações de módulos devem ser realizadas através do botão de atualização modular.");
            }

            var err = await response.Content.ReadFromJsonAsync<ApiErrorDto>(cancellationToken: cancellationToken);
            if (!string.IsNullOrWhiteSpace(err?.Message))
            {
                throw new InvalidOperationException(err.Message);
            }
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Falha de comunicação ao gerar checkout: {ex.Message}", ex);
        }

        return null;
    }

    public async Task<UpdateModularSubscriptionResultModel?> UpdateModularSubscriptionAsync(
        UpdateModularSubscriptionRequestModel request,
        CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        var response = await _httpClient.PostAsJsonAsync("/api/v1/tenancy/subscription/update-modular", request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<UpdateModularSubscriptionResultModel>(cancellationToken: cancellationToken);
        }

        var errorObj = await response.Content.ReadFromJsonAsync<ApiErrorDto>(cancellationToken: cancellationToken);
        throw new InvalidOperationException(errorObj?.Message ?? $"Erro ao atualizar assinatura modular (Código: {response.StatusCode}).");
    }

    public async Task<StripePortalSessionResponse?> CreateSubscriptionPortalAsync(
        Guid tenantId,
        string? returnUrl = null,
        CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        try
        {
            var payload = new
            {
                tenantId,
                returnUrl
            };
            var response = await _httpClient.PostAsJsonAsync("/api/v1/tenancy/subscription/portal", payload, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<StripePortalSessionResponse>(cancellationToken: cancellationToken);
            }

            try
            {
                var errorObj = await response.Content.ReadFromJsonAsync<ApiErrorDto>(cancellationToken: cancellationToken);
                if (errorObj != null && !string.IsNullOrWhiteSpace(errorObj.Message))
                {
                    throw new InvalidOperationException(errorObj.Message);
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch
            {
                // Silently fallback if body cannot be parsed as ApiErrorDto
            }
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch
        {
        }

        return null;
    }

    public async Task<List<ProductionUnitModel>> GetProductionUnitsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        try
        {
            var units = await _httpClient.GetFromJsonAsync<List<ProductionUnitModel>>($"/api/v1/tenancy/production-units?tenantId={tenantId}", cancellationToken);
            if (units is not null)
            {
                return units;
            }
        }
        catch
        {
            // Return empty list on failure
        }

        return new List<ProductionUnitModel>();
    }

    public async Task<ProductionUnitModel?> CreateProductionUnitAsync(CreateProductionUnitRequest request, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/v1/tenancy/production-units", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<ProductionUnitModel>(cancellationToken: cancellationToken);
            }
        }
        catch
        {
        }

        return null;
    }

    public async Task<TeamOverviewModel> GetTeamMembersAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        try
        {
            var overview = await _httpClient.GetFromJsonAsync<TeamOverviewModel>($"/api/v1/tenancy/members?tenantId={tenantId}", cancellationToken);
            if (overview is not null)
            {
                return overview;
            }
        }
        catch
        {
            // Return empty list on failure
        }

        return new TeamOverviewModel(new List<TeamMemberModel>(), new List<TeamInviteModel>());
    }

    public async Task<TeamInviteModel?> InviteTeamMemberAsync(InviteTeamMemberRequest request, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/v1/tenancy/invites", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<TeamInviteModel>(cancellationToken: cancellationToken);
            }
        }
        catch
        {
        }

        return null;
    }

    public async Task<bool> RevokeInviteAsync(Guid tenantId, Guid inviteId, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        try
        {
            var response = await _httpClient.DeleteAsync($"/api/v1/tenancy/invites/{inviteId}?tenantId={tenantId}", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> RemoveTeamMemberAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        try
        {
            var response = await _httpClient.DeleteAsync($"/api/v1/tenancy/members/{userId}?tenantId={tenantId}", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<UserProfileModel?> GetUserProfileAsync(CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        try
        {
            var response = await _httpClient.GetAsync("/api/v1/auth/profile", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<UserProfileModel>(cancellationToken: cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    public async Task<(bool Success, UserProfileModel? Profile, string? ErrorMessage)> UpdateUserProfileAsync(
        UpdateUserProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        try
        {
            var response = await _httpClient.PutAsJsonAsync("/api/v1/auth/profile", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var profile = await response.Content.ReadFromJsonAsync<UserProfileModel>(cancellationToken: cancellationToken);
                return (true, profile, null);
            }

            var error = await response.Content.ReadFromJsonAsync<ApiErrorDto>(cancellationToken: cancellationToken);
            return (false, null, error?.Message ?? "Erro ao atualizar dados do perfil.");
        }
        catch (Exception ex)
        {
            return (false, null, ex.Message);
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> ChangePasswordAsync(
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/v1/auth/change-password", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return (true, null);
            }

            var error = await response.Content.ReadFromJsonAsync<ApiErrorDto>(cancellationToken: cancellationToken);
            return (false, error?.Message ?? "Erro ao alterar senha.");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool Success, byte[]? Content, string? FileName, string? ErrorMessage)> ExportUserDataPortabilityAsync(
        CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        try
        {
            var response = await _httpClient.GetAsync("/api/v1/auth/my-data/export", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                var fileName = response.Content.Headers.ContentDisposition?.FileNameStar 
                            ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                            ?? $"meus-dados-lgpd-{DateTime.UtcNow:yyyyMMddHHmmss}.json";

                return (true, bytes, fileName, null);
            }

            var error = await response.Content.ReadFromJsonAsync<ApiErrorDto>(cancellationToken: cancellationToken);
            return (false, null, null, error?.Message ?? "Erro ao exportar dados pessoais.");
        }
        catch (Exception ex)
        {
            return (false, null, null, ex.Message);
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> AnonymizeAccountAsync(
        AnonymizeAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/v1/auth/account/delete", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return (true, null);
            }

            var error = await response.Content.ReadFromJsonAsync<ApiErrorDto>(cancellationToken: cancellationToken);
            return (false, error?.Message ?? "Erro ao encerrar e anonimizar conta.");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool Success, DataSubjectRequestReceiptModel? Receipt, string? ErrorMessage)> SubmitDataSubjectRequestAsync(
        SubmitDataSubjectRequestDto request,
        CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/v1/compliance/dpo/requests", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var receipt = await response.Content.ReadFromJsonAsync<DataSubjectRequestReceiptModel>(cancellationToken: cancellationToken);
                return (true, receipt, null);
            }

            var error = await response.Content.ReadFromJsonAsync<ApiErrorDto>(cancellationToken: cancellationToken);
            return (false, null, error?.Message ?? "Erro ao registrar solicitação formal de privacidade.");
        }
        catch (Exception ex)
        {
            return (false, null, ex.Message);
        }
    }

    private async Task AttachTokenAsync()
    {
        if (_jsRuntime == null) return;
        try
        {
            var token = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "authToken");
            _httpClient.DefaultRequestHeaders.Authorization = string.IsNullOrWhiteSpace(token)
                ? null
                : new AuthenticationHeaderValue("Bearer", token);
        }
        catch
        {
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }
    }

    public static TeamOverviewModel GetFallbackTeamOverview(Guid tenantId)
    {
        return new TeamOverviewModel(
            Members: new List<TeamMemberModel>
            {
                new(Guid.NewGuid(), "admin@santafe.com.br", "Roberto Almeida (Proprietário)", 1, DateTime.UtcNow.AddMonths(-12), true),
                new(Guid.NewGuid(), "carlos.zootecnia@santafe.com.br", "Dr. Carlos Eduardo", 2, DateTime.UtcNow.AddMonths(-6), true),
                new(Guid.NewGuid(), "dra.mariana.vet@santafe.com.br", "Dra. Mariana Santos", 3, DateTime.UtcNow.AddMonths(-3), true),
                new(Guid.NewGuid(), "tiago.curral@santafe.com.br", "Tiago Peão Curral", 4, DateTime.UtcNow.AddMonths(-1), true)
            },
            PendingInvites: new List<TeamInviteModel>
            {
                new(Guid.NewGuid(), tenantId, "consultor.nutricao@agro.com.br", 2, "TK-998822", DateTime.UtcNow, DateTime.UtcNow.AddDays(5), false)
            }
        );
    }

    public static TenantProfileModel GetFallbackProfile(Guid tenantId)
    {
        return new TenantProfileModel(
            tenantId,
            "Fazenda Santa Fé - Matriz",
            "12.345.678/0001-99",
            "Active",
            "Enterprise",
            12500,
            "MT",
            "Sorriso",
            "IE-99887766-0",
            4500.00m,
            "Recria e Engorda"
        );
    }

    public static List<ProductionUnitModel> GetFallbackProductionUnits(Guid tenantId)
    {
        return new List<ProductionUnitModel>
        {
            new(Guid.NewGuid(), tenantId, "UN-001-SFE", "Unidade Matriz 01", "Gestação", "Active", 5000, 4100, "Rodovia BR-163 KM 450", 82.0m),
            new(Guid.NewGuid(), tenantId, "UN-002-SFE", "Crechário Sul", "Creche", "Active", 2500, 2350, "Setor Sul Piquete 4", 94.0m),
            new(Guid.NewGuid(), tenantId, "UN-004-SFE", "Unidade de Engorda 04", "Confinamento", "Maintenance", 5000, 0, "Curral Central", 0m)
        };
    }

    public static List<SubscriptionPlanModel> GetFallbackPlans()
    {
        return new List<SubscriptionPlanModel>
        {
            new(
                PlanId: "Starter",
                Name: "Starter Pecuária",
                Description: "Ideal para pequenas propriedades iniciando o controle de plantel e reprodução.",
                MonthlyPrice: 149.00m,
                AnnualPriceMonthly: 119.00m,
                HeadCapacityLimit: 500,
                IncludedModules: new[] { "Cows", "Breeding", "Calving" },
                IsPopular: false,
                Features: new List<SubscriptionPlanFeatureModel>
                {
                    new("Modules.Cows", "Módulo de Plantel Bovino & Matrizes", true),
                    new("Modules.Breeding", "Módulo de Reprodução & IATF", true),
                    new("Modules.Calving", "Módulo de Partos & Bezerreiro", true),
                    new("PwaOfflineMode", "Modo Offline PWA em Curral", true)
                }
            ),
            new(
                PlanId: "Pro",
                Name: "Pro Fazenda",
                Description: "Gestão completa de pasto, balança de curral, manejo reprodutivo e sanidade.",
                MonthlyPrice: 349.00m,
                AnnualPriceMonthly: 279.00m,
                HeadCapacityLimit: 2500,
                IncludedModules: new[] { "Cows", "Breeding", "Calving", "Growth", "Nutrition", "Sanitary" },
                IsPopular: true,
                Features: new List<SubscriptionPlanFeatureModel>
                {
                    new("Modules.Cows", "Módulo de Plantel Bovino & Matrizes", true),
                    new("Modules.Breeding", "Módulo de Reprodução & IATF", true),
                    new("Modules.Calving", "Módulo de Partos & Bezerreiro", true),
                    new("Modules.Growth", "Módulo de Manejo & Pesagem", true),
                    new("Modules.Sanitary", "Módulo Sanitário & Vacinação", true),
                    new("Modules.Nutrition", "Módulo Nutricional & Suplementação", true),
                    new("PwaOfflineMode", "Modo Offline PWA em Curral", true)
                }
            ),
            new(
                PlanId: "Enterprise",
                Name: "Enterprise Confinamento",
                Description: "Para grandes grupos pecuários, confinamentos e análise avançada de custo por @.",
                MonthlyPrice: 799.00m,
                AnnualPriceMonthly: 649.00m,
                HeadCapacityLimit: int.MaxValue,
                IncludedModules: new[] { "Cows", "Breeding", "Calving", "Growth", "Nutrition", "Sanitary", "Feedlot", "Analytics" },
                IsPopular: false,
                Features: new List<SubscriptionPlanFeatureModel>
                {
                    new("Modules.Cows", "Módulo de Plantel Bovino & Matrizes", true),
                    new("Modules.Breeding", "Módulo de Reprodução & IATF", true),
                    new("Modules.Calving", "Módulo de Partos & Bezerreiro", true),
                    new("Modules.Growth", "Módulo de Manejo & Pesagem", true),
                    new("Modules.Sanitary", "Módulo Sanitário & Vacinação", true),
                    new("Modules.Nutrition", "Módulo Nutricional & Suplementação", true),
                    new("Modules.Feedlot", "Módulo de Confinamento & Trato TMR", true),
                    new("Modules.Analytics", "Zootecnia Avançada & Analytics", true),
                    new("PwaOfflineMode", "Modo Offline PWA em Curral", true)
                }
            )
        };
    }

    public static ModularSubscriptionCatalogModel GetDefaultModularCatalog()
    {
        var tiers = new List<HeadTierOptionModel>
        {
            new("TIER_500", "Até 500 cabeças", 1, 500, 1),
            new("TIER_1500", "501 a 1.500 cabeças", 501, 1500, 2),
            new("TIER_3000", "1.501 a 3.000 cabeças", 1501, 3000, 3),
            new("TIER_5000_PLUS", "+3.000 cabeças", 3001, 10000, 4)
        };

        var modules = new List<CommercialModuleOptionModel>
        {
            new("Cows", "Gestão do Plantel", "Cadastro zootécnico individual, genealogia, rastreabilidade e histórico produtivo.", "pets", true, 1, new List<ModuleTierPriceOptionModel>
            {
                new("TIER_500", 149.00m, 126.65m),
                new("TIER_1500", 229.00m, 194.65m),
                new("TIER_3000", 349.00m, 296.65m),
                new("TIER_5000_PLUS", 499.00m, 424.15m)
            }),
            new("Breeding", "Reprodução & IATF", "Protocolos de IATF, diagnóstico gestacional, touros e índices reprodutivos.", "favorite", false, 2, new List<ModuleTierPriceOptionModel>
            {
                new("TIER_500", 79.00m, 67.15m),
                new("TIER_1500", 119.00m, 101.15m),
                new("TIER_3000", 169.00m, 143.65m),
                new("TIER_5000_PLUS", 239.00m, 203.15m)
            }),
            new("Calving", "Maternidade & Bezerreiro", "Nascimentos, desmama ajustada (P205), mortalidade pré-desmama e lotes de cria.", "child_care", false, 3, new List<ModuleTierPriceOptionModel>
            {
                new("TIER_500", 49.00m, 41.65m),
                new("TIER_1500", 79.00m, 67.15m),
                new("TIER_3000", 119.00m, 101.15m),
                new("TIER_5000_PLUS", 169.00m, 143.65m)
            }),
            new("Growth", "Recria & Engorda", "Pesagens periódicas, Ganho de Peso Diário (GPD), UA/ha e taxa de lotação.", "trending_up", false, 4, new List<ModuleTierPriceOptionModel>
            {
                new("TIER_500", 69.00m, 58.65m),
                new("TIER_1500", 99.00m, 84.15m),
                new("TIER_3000", 149.00m, 126.65m),
                new("TIER_5000_PLUS", 209.00m, 177.65m)
            }),
            new("Sanitary", "Sanitário & Carência", "Campanhas de vacinação, histórico sanitário e bloqueio por carência de abate.", "vaccines", false, 5, new List<ModuleTierPriceOptionModel>
            {
                new("TIER_500", 49.00m, 41.65m),
                new("TIER_1500", 69.00m, 58.65m),
                new("TIER_3000", 99.00m, 84.15m),
                new("TIER_5000_PLUS", 139.00m, 118.15m)
            }),
            new("Nutrition", "Nutrição & Confinamento", "Trato diário, conversão alimentar, sal mineral, silagem e custo de ração.", "restaurant", false, 6, new List<ModuleTierPriceOptionModel>
            {
                new("TIER_500", 59.00m, 50.15m),
                new("TIER_1500", 89.00m, 75.65m),
                new("TIER_3000", 129.00m, 109.65m),
                new("TIER_5000_PLUS", 179.00m, 152.15m)
            }),
            new("Analytics", "Zootecnia & Analytics", "Métricas avançadas, benchmarking zootécnico, custo da @ produzida e relatórios.", "insights", false, 7, new List<ModuleTierPriceOptionModel>
            {
                new("TIER_500", 69.00m, 58.65m),
                new("TIER_1500", 99.00m, 84.15m),
                new("TIER_3000", 149.00m, 126.65m),
                new("TIER_5000_PLUS", 209.00m, 177.65m)
            })
        };

        return new ModularSubscriptionCatalogModel(tiers, modules);
    }
}
