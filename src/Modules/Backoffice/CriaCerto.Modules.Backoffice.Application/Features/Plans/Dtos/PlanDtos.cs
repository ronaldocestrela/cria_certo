namespace CriaCerto.Modules.Backoffice.Application.Features.Plans.Dtos;

public sealed record PlanFeatureDto(
    Guid Id,
    string FeatureKey,
    string DisplayName,
    bool IsEnabled,
    string FeatureType
);

public sealed record PlanLimitDto(
    Guid Id,
    string LimitKey,
    decimal LimitValue,
    string Unit
);

public sealed record PlanVersionDto(
    Guid Id,
    Guid PlanCatalogId,
    int VersionNumber,
    string VersionName,
    string Status,
    decimal MonthlyPrice,
    decimal AnnualPriceMonthly,
    int HeadCapacityLimit,
    int? MaxUsers,
    int? MaxProductionUnits,
    DateTimeOffset? EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    DateTimeOffset? PublishedAtUtc,
    Guid? PublishedByAdminId,
    string? ApprovalNotes,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<PlanFeatureDto> Features,
    IReadOnlyList<PlanLimitDto> Limits
);

public sealed record PlanCatalogDto(
    Guid Id,
    string Code,
    string Name,
    string Description,
    string Category,
    bool IsArchived,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    PlanVersionDto? ActiveVersion,
    PlanVersionDto? DraftVersion,
    IReadOnlyList<PlanVersionDto> Versions
);

public sealed record PlanFeatureInputDto(
    string FeatureKey,
    string DisplayName,
    bool IsEnabled = true,
    string FeatureType = "ModuleAccess"
);

public sealed record PlanLimitInputDto(
    string LimitKey,
    decimal LimitValue,
    string Unit
);

public sealed record PlanVersionComparisonDto(
    PlanVersionDto BaseVersion,
    PlanVersionDto TargetVersion,
    IReadOnlyList<string> AddedFeatures,
    IReadOnlyList<string> RemovedFeatures,
    IReadOnlyList<string> ChangedLimits,
    decimal PriceDifferenceMonthly,
    decimal PriceDifferenceAnnual
);
