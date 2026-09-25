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
    string Type
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

public sealed record ChangeSubscriptionPlanRequest(
    Guid TenantId,
    string NewPlan
);

public sealed record ChangeSubscriptionPlanResponse(
    string Token,
    TenantProfileModel Profile
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

    public async Task<ChangeSubscriptionPlanResponse?> ChangeSubscriptionPlanAsync(ChangeSubscriptionPlanRequest request, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        try
        {
            var response = await _httpClient.PutAsJsonAsync("/api/v1/tenancy/subscription", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<ChangeSubscriptionPlanResponse>(cancellationToken: cancellationToken);
            }
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
                IncludedModules: new[] { "Breeding", "Calving" },
                IsPopular: false,
                Features: new List<SubscriptionPlanFeatureModel>
                {
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
                IncludedModules: new[] { "Breeding", "Calving", "Growth", "Nutrition", "Sanitary" },
                IsPopular: true,
                Features: new List<SubscriptionPlanFeatureModel>
                {
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
                IncludedModules: new[] { "Breeding", "Calving", "Growth", "Nutrition", "Sanitary", "Analytics" },
                IsPopular: false,
                Features: new List<SubscriptionPlanFeatureModel>
                {
                    new("Modules.Breeding", "Módulo de Reprodução & IATF", true),
                    new("Modules.Calving", "Módulo de Partos & Bezerreiro", true),
                    new("Modules.Growth", "Módulo de Manejo & Pesagem", true),
                    new("Modules.Sanitary", "Módulo Sanitário & Vacinação", true),
                    new("Modules.Nutrition", "Módulo Nutricional & Suplementação", true),
                    new("Modules.Analytics", "Zootecnia Avançada & Analytics", true),
                    new("PwaOfflineMode", "Modo Offline PWA em Curral", true)
                }
            )
        };
    }
}
