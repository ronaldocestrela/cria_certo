using CriaCerto.Web.Client.Models;

namespace CriaCerto.Web.Client.Services;

public interface IFeatureFlagClientService
{
    event Action? OnFlagsChanged;

    Task<bool> IsFeatureEnabledAsync(string flagKey);
    Task<List<FeatureFlagClientDto>> GetFlagsAsync(bool forceRefresh = false);
    Task<FeatureFlagClientDto?> GetFlagByKeyAsync(string key);
    Task<List<RolloutSloHealthClientDto>> GetSloHealthAsync();
    Task<FeatureFlagClientDto?> ToggleFlagAsync(string key, bool isEnabled, string? reason);
    Task<FeatureFlagClientDto?> UpdateRolloutAsync(string key, int percentage, int maxAllowedRing, List<string>? whitelistedEmails, string? reason);
    Task<FeatureFlagClientDto?> TriggerKillSwitchAsync(string key, string reason);
    Task<FeatureFlagClientDto?> RestoreKillSwitchAsync(string key, string reason);
}
