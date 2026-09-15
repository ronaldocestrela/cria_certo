using CriaCerto.Modules.Backoffice.Application.Domain.Enums;

namespace CriaCerto.Modules.Backoffice.Application.Features.Rollout.Dtos;

public record FeatureFlagDto(
    Guid Id,
    string Key,
    string Name,
    string Description,
    FeatureFlagCategory Category,
    bool IsEnabled,
    int RolloutPercentage,
    RolloutRing MaxAllowedRing,
    IReadOnlyList<string> WhitelistedEmails,
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

public record RolloutSloHealthDto(
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

public record ToggleFeatureFlagRequest(
    bool IsEnabled,
    string? Reason
);

public record UpdateRolloutWaveRequest(
    int Percentage,
    RolloutRing MaxAllowedRing,
    List<string>? WhitelistedEmails,
    string? Reason
);

public record TriggerKillSwitchRequest(
    string Reason
);

public record RestoreKillSwitchRequest(
    string Reason
);
