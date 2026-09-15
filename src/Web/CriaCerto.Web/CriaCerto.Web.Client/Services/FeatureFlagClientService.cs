using System.Net.Http.Json;
using CriaCerto.Web.Client.Models;

namespace CriaCerto.Web.Client.Services;

public class FeatureFlagClientService : IFeatureFlagClientService
{
    private readonly HttpClient _httpClient;
    private readonly IBackofficePermissionService _permissionService;
    private readonly Dictionary<string, FeatureFlagClientDto> _cache = new(StringComparer.OrdinalIgnoreCase);
    private DateTime? _lastFetchUtc;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(2);

    public event Action? OnFlagsChanged;

    public FeatureFlagClientService(
        HttpClient httpClient,
        IBackofficePermissionService permissionService)
    {
        _httpClient = httpClient;
        _permissionService = permissionService;
    }

    public async Task<bool> IsFeatureEnabledAsync(string flagKey)
    {
        if (string.IsNullOrWhiteSpace(flagKey))
            return true;

        var flags = await GetFlagsAsync();
        var normalized = flagKey.Trim().ToLowerInvariant();

        if (!_cache.TryGetValue(normalized, out var flag))
        {
            // Se a flag não existir no catálogo, por padrão seguro retorna true (não bloqueia features comuns)
            return true;
        }

        if (flag.KillSwitchActive || !flag.IsEnabled)
            return false;

        // Se está em 100% e GA
        if (flag.RolloutPercentage >= 100 && flag.MaxAllowedRing.Contains("Ring2", StringComparison.OrdinalIgnoreCase))
            return true;

        var role = await _permissionService.GetCurrentUserRoleAsync();

        if (flag.MaxAllowedRing.Contains("Ring0", StringComparison.OrdinalIgnoreCase))
        {
            return role.Equals("PlatformOwner", StringComparison.OrdinalIgnoreCase) ||
                   role.Equals("Admin", StringComparison.OrdinalIgnoreCase);
        }

        if (flag.MaxAllowedRing.Contains("Ring1", StringComparison.OrdinalIgnoreCase))
        {
            return role.Equals("PlatformOwner", StringComparison.OrdinalIgnoreCase) ||
                   role.Equals("Admin", StringComparison.OrdinalIgnoreCase) ||
                   role.Equals("SupportN2", StringComparison.OrdinalIgnoreCase) ||
                   role.Equals("FinanceOps", StringComparison.OrdinalIgnoreCase);
        }

        return true;
    }

    public async Task<List<FeatureFlagClientDto>> GetFlagsAsync(bool forceRefresh = false)
    {
        if (!forceRefresh && _lastFetchUtc.HasValue && (DateTime.UtcNow - _lastFetchUtc.Value) < CacheDuration && _cache.Count > 0)
        {
            return _cache.Values.ToList();
        }

        try
        {
            var response = await _httpClient.GetAsync("api/v1/backoffice/feature-flags");
            if (response.IsSuccessStatusCode)
            {
                var flags = await response.Content.ReadFromJsonAsync<List<FeatureFlagClientDto>>();
                if (flags is not null)
                {
                    _cache.Clear();
                    foreach (var f in flags)
                    {
                        _cache[f.Key] = f;
                    }
                    _lastFetchUtc = DateTime.UtcNow;
                    return flags;
                }
            }
        }
        catch
        {
            // Fallback para cache local existente se houver erro transitório de rede
        }

        return _cache.Values.ToList();
    }

    public async Task<FeatureFlagClientDto?> GetFlagByKeyAsync(string key)
    {
        await GetFlagsAsync();
        _cache.TryGetValue(key.Trim().ToLowerInvariant(), out var flag);
        return flag;
    }

    public async Task<List<RolloutSloHealthClientDto>> GetSloHealthAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/v1/backoffice/feature-flags/slo-health");
            if (response.IsSuccessStatusCode)
            {
                var health = await response.Content.ReadFromJsonAsync<List<RolloutSloHealthClientDto>>();
                return health ?? new List<RolloutSloHealthClientDto>();
            }
        }
        catch
        {
            // Retorna lista vazia em caso de falha transitória
        }

        return new List<RolloutSloHealthClientDto>();
    }

    public async Task<FeatureFlagClientDto?> ToggleFlagAsync(string key, bool isEnabled, string? reason)
    {
        var request = new ToggleFeatureFlagClientRequest(isEnabled, reason);
        var response = await _httpClient.PostAsJsonAsync($"api/v1/backoffice/feature-flags/{Uri.EscapeDataString(key)}/toggle", request);
        if (!response.IsSuccessStatusCode) return null;

        var updated = await response.Content.ReadFromJsonAsync<FeatureFlagClientDto>();
        if (updated is not null)
        {
            _cache[updated.Key] = updated;
            OnFlagsChanged?.Invoke();
        }
        return updated;
    }

    public async Task<FeatureFlagClientDto?> UpdateRolloutAsync(string key, int percentage, int maxAllowedRing, List<string>? whitelistedEmails, string? reason)
    {
        var request = new UpdateRolloutWaveClientRequest(percentage, maxAllowedRing, whitelistedEmails, reason);
        var response = await _httpClient.PutAsJsonAsync($"api/v1/backoffice/feature-flags/{Uri.EscapeDataString(key)}/rollout", request);
        if (!response.IsSuccessStatusCode) return null;

        var updated = await response.Content.ReadFromJsonAsync<FeatureFlagClientDto>();
        if (updated is not null)
        {
            _cache[updated.Key] = updated;
            OnFlagsChanged?.Invoke();
        }
        return updated;
    }

    public async Task<FeatureFlagClientDto?> TriggerKillSwitchAsync(string key, string reason)
    {
        var request = new TriggerKillSwitchClientRequest(reason);
        var response = await _httpClient.PostAsJsonAsync($"api/v1/backoffice/feature-flags/{Uri.EscapeDataString(key)}/kill-switch", request);
        if (!response.IsSuccessStatusCode) return null;

        var updated = await response.Content.ReadFromJsonAsync<FeatureFlagClientDto>();
        if (updated is not null)
        {
            _cache[updated.Key] = updated;
            OnFlagsChanged?.Invoke();
        }
        return updated;
    }

    public async Task<FeatureFlagClientDto?> RestoreKillSwitchAsync(string key, string reason)
    {
        var request = new RestoreKillSwitchClientRequest(reason);
        var response = await _httpClient.PostAsJsonAsync($"api/v1/backoffice/feature-flags/{Uri.EscapeDataString(key)}/restore", request);
        if (!response.IsSuccessStatusCode) return null;

        var updated = await response.Content.ReadFromJsonAsync<FeatureFlagClientDto>();
        if (updated is not null)
        {
            _cache[updated.Key] = updated;
            OnFlagsChanged?.Invoke();
        }
        return updated;
    }
}
