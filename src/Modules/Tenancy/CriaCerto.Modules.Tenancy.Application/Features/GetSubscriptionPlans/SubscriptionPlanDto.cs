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
    IReadOnlyList<SubscriptionPlanFeatureDto>? Features = null
);
