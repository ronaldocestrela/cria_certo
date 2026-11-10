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
    IReadOnlyCollection<string> RemovedFeatures,
    string? CurrentHeadTierCode = null,
    string? TargetHeadTierCode = null,
    IReadOnlyCollection<string>? CurrentSubscribedModules = null,
    IReadOnlyCollection<string>? TargetSubscribedModules = null
);

public sealed record ChangeTenantPlanRequestDto(
    Guid? TargetPlanVersionId = null,
    string Justification = "",
    bool ForceImmediate = false,
    string? HeadTierCode = null,
    IReadOnlyCollection<string>? SubscribedModules = null
);

public sealed record ChangeTenantPlanResponseDto(
    Guid TenantId,
    Guid AppliedPlanVersionId,
    string PlanName,
    string SubscriptionStatus,
    bool GracePeriodActivated,
    DateTime? GracePeriodEndsAtUtc,
    string Message,
    string? HeadTierCode = null,
    IReadOnlyCollection<string>? SubscribedModules = null,
    int? HeadCapacity = null
);

public sealed record ResolveGracePeriodRequestDto(
    string Justification
);
