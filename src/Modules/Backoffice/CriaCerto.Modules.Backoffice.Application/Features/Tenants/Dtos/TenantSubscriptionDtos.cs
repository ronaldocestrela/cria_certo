namespace CriaCerto.Modules.Backoffice.Application.Features.Tenants.Dtos;

public sealed record TenantPlanPreviewDto(
    Guid TenantId,
    string TenantName,
    Guid CurrentPlanVersionId,
    string CurrentPlanName,
    int CurrentHeadCapacity,
    int CurrentHeadCountUsage,
    Guid TargetPlanVersionId,
    string TargetPlanName,
    int TargetHeadCapacity,
    int DeltaHeadCapacity,
    bool RequiresGracePeriod,
    int GracePeriodDays,
    IReadOnlyCollection<string> AddedFeatures,
    IReadOnlyCollection<string> RemovedFeatures
);

public sealed record ChangeTenantPlanRequestDto(
    Guid TargetPlanVersionId,
    string Justification,
    bool ForceImmediate = false
);

public sealed record ChangeTenantPlanResponseDto(
    Guid TenantId,
    Guid AppliedPlanVersionId,
    string PlanName,
    string SubscriptionStatus,
    bool GracePeriodActivated,
    DateTime? GracePeriodEndsAtUtc,
    string Message
);

public sealed record ResolveGracePeriodRequestDto(
    string Justification
);
