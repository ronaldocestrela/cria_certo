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

public sealed record HeadTierCatalogDto(
    Guid Id,
    string TierCode,
    string DisplayName,
    int MinHeads,
    int MaxHeads,
    int SortOrder,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc
);

public sealed record ModuleTierPriceDto(
    Guid Id,
    Guid ModuleCatalogId,
    Guid HeadTierCatalogId,
    string TierCode,
    int VersionNumber,
    string Status,
    decimal MonthlyPrice,
    decimal AnnualPriceMonthly,
    string? StripePriceIdMonthly,
    string? StripePriceIdAnnual,
    DateTimeOffset? PublishedAtUtc,
    Guid? PublishedByAdminId,
    string? ApprovalNotes,
    DateTimeOffset CreatedAtUtc
);

public sealed record ModuleCatalogDto(
    Guid Id,
    string ModuleKey,
    string Name,
    string Description,
    string Icon,
    bool IsCore,
    bool IsActive,
    int SortOrder,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<ModuleTierPriceDto> ActivePrices,
    IReadOnlyList<ModuleTierPriceDto> DraftPrices,
    IReadOnlyList<ModuleTierPriceDto> PriceHistory
);

public sealed record ModuleTierPriceInputDto(
    Guid HeadTierCatalogId,
    decimal MonthlyPrice,
    decimal AnnualPriceMonthly,
    string? StripePriceIdMonthly = null,
    string? StripePriceIdAnnual = null
);
