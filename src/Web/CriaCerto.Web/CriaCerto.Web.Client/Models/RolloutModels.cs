namespace CriaCerto.Web.Client.Models;

public record FeatureFlagClientDto(
    Guid Id,
    string Key,
    string Name,
    string Description,
    string Category,
    bool IsEnabled,
    int RolloutPercentage,
    string MaxAllowedRing,
    List<string> WhitelistedEmails,
    bool KillSwitchActive,
    string? KillSwitchReason,
    DateTime? KillSwitchActivatedAtUtc,
    string? KillSwitchActivatedBy,
    DateTime CreatedAtUtc,
    string CreatedBy,
    DateTime? UpdatedAtUtc,
    string? UpdatedBy,
    string? LastToggledReason
);

public record RolloutSloHealthClientDto(
    string FlagKey,
    string FlagName,
    bool IsHealthy,
    double CurrentErrorRatePercent,
    double MaxErrorRateThresholdPercent,
    double CurrentLatencyP95Ms,
    double MaxLatencyP95ThresholdMs,
    int PolicyFailuresCount,
    int TotalEvaluationsCount,
    int TotalBlockedCount,
    string StatusSummary
);

public record ToggleFeatureFlagClientRequest(
    bool IsEnabled,
    string? Reason
);

public record UpdateRolloutWaveClientRequest(
    int Percentage,
    int MaxAllowedRing,
    List<string>? WhitelistedEmails,
    string? Reason
);

public record TriggerKillSwitchClientRequest(
    string Reason
);

public record RestoreKillSwitchClientRequest(
    string Reason
);
