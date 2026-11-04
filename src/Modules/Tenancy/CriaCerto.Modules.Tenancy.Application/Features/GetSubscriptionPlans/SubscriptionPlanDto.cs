namespace CriaCerto.Modules.Tenancy.Application.Features.GetSubscriptionPlans;

public sealed record SubscriptionPlanFeatureDto(
    string Key,
    string Name,
    bool IsEnabled = true
);

public sealed record SubscriptionPlanDto(
    string PlanId,
    string Name,
    string Description,
    decimal MonthlyPrice,
    decimal AnnualPriceMonthly,
    int HeadCapacityLimit,
    IReadOnlyList<string> IncludedModules,
    bool IsPopular,
    IReadOnlyList<SubscriptionPlanFeatureDto>? Features = null,
    string? StripePriceIdMonthly = null,
    string? StripePriceIdAnnual = null
);

public sealed record HeadTierOptionDto(
    string TierCode,
    string DisplayName,
    int MinHeads,
    int MaxHeads,
    int SortOrder
);

public sealed record ModuleTierPriceOptionDto(
    string TierCode,
    decimal MonthlyPrice,
    decimal AnnualPriceMonthly,
    string? StripePriceIdMonthly = null,
    string? StripePriceIdAnnual = null
);

public sealed record CommercialModuleOptionDto(
    string ModuleKey,
    string Name,
    string Description,
    string Icon,
    bool IsCore,
    int SortOrder,
    IReadOnlyList<ModuleTierPriceOptionDto> PricesByTier
);

public sealed record ModularSubscriptionCatalogDto(
    IReadOnlyList<HeadTierOptionDto> HeadTiers,
    IReadOnlyList<CommercialModuleOptionDto> Modules
);
