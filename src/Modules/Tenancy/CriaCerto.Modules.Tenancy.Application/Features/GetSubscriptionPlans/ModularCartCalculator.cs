using System;
using System.Collections.Generic;
using System.Linq;
using CriaCerto.Modules.Tenancy.Application.Domain;

namespace CriaCerto.Modules.Tenancy.Application.Features.GetSubscriptionPlans;

public sealed record ModularCartItemCalculation(
    string ModuleKey,
    string ModuleName,
    decimal MonthlyPrice,
    decimal AnnualPriceMonthly,
    string? StripePriceIdMonthly,
    string? StripePriceIdAnnual
);

public sealed record ModularCartSummaryCalculation(
    string HeadTierCode,
    int TierMaxHeads,
    bool IsAnnual,
    IReadOnlyList<ModularCartItemCalculation> Items,
    decimal TotalMonthlyEquivalent,
    decimal TotalAnnualBilledAmount,
    decimal CostPerHeadPerMonth,
    bool IsTierDisabled
);

public static class ModularCartCalculator
{
    public const string CoreModuleKey = HeadTierCapacityLimits.CoreModuleKey;

    public static ModularCartSummaryCalculation Calculate(
        ModularSubscriptionCatalogDto catalog,
        string rawTierCode,
        IEnumerable<string>? selectedModules,
        bool isAnnual,
        int activeHerdCount = 0)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        string tierCode = HeadTierCapacityLimits.IsValidHeadTierCode(rawTierCode)
            ? HeadTierCapacityLimits.NormalizeHeadTierCode(rawTierCode)
            : HeadTierCapacityLimits.Tier500;

        int tierMaxHeads = HeadTierCapacityLimits.GetMaxHeadsForTier(tierCode);
        bool isTierDisabled = activeHerdCount > 0 && activeHerdCount > tierMaxHeads;

        var normalizedModuleKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            CoreModuleKey
        };

        if (selectedModules != null)
        {
            foreach (var mod in selectedModules)
            {
                if (string.IsNullOrWhiteSpace(mod))
                {
                    continue;
                }

                var canonical = HeadTierCapacityLimits.NormalizeCommercialModuleKey(mod);
                if (canonical != null)
                {
                    normalizedModuleKeys.Add(canonical);
                }
            }
        }

        var orderedTargetKeys = HeadTierCapacityLimits.AllCommercialModules
            .Where(normalizedModuleKeys.Contains)
            .ToList();

        var items = new List<ModularCartItemCalculation>();
        decimal totalMonthlyEquivalent = 0m;
        decimal totalAnnualBilled = 0m;

        foreach (var moduleKey in orderedTargetKeys)
        {
            var moduleOption = catalog.Modules.FirstOrDefault(m => string.Equals(m.ModuleKey, moduleKey, StringComparison.OrdinalIgnoreCase));
            var priceOption = moduleOption?.PricesByTier.FirstOrDefault(p => string.Equals(p.TierCode, tierCode, StringComparison.OrdinalIgnoreCase))
                ?? moduleOption?.PricesByTier.FirstOrDefault();

            decimal monthlyPrice = priceOption?.MonthlyPrice ?? 0m;
            decimal annualPriceMonthly = priceOption?.AnnualPriceMonthly ?? Math.Round(monthlyPrice * 0.80m, 2);
            string? stripePriceMonthly = priceOption?.StripePriceIdMonthly;
            string? stripePriceAnnual = priceOption?.StripePriceIdAnnual;

            items.Add(new ModularCartItemCalculation(
                ModuleKey: moduleKey,
                ModuleName: moduleOption?.Name ?? moduleKey,
                MonthlyPrice: monthlyPrice,
                AnnualPriceMonthly: annualPriceMonthly,
                StripePriceIdMonthly: stripePriceMonthly,
                StripePriceIdAnnual: stripePriceAnnual
            ));

            if (isAnnual)
            {
                totalMonthlyEquivalent += annualPriceMonthly;
                totalAnnualBilled += annualPriceMonthly * 12m;
            }
            else
            {
                totalMonthlyEquivalent += monthlyPrice;
                totalAnnualBilled += monthlyPrice * 12m;
            }
        }

        int denominator = activeHerdCount > 0 ? activeHerdCount : Math.Max(1, tierMaxHeads);
        decimal costPerHead = Math.Round(totalMonthlyEquivalent / denominator, 2);

        return new ModularCartSummaryCalculation(
            HeadTierCode: tierCode,
            TierMaxHeads: tierMaxHeads,
            IsAnnual: isAnnual,
            Items: items,
            TotalMonthlyEquivalent: totalMonthlyEquivalent,
            TotalAnnualBilledAmount: totalAnnualBilled,
            CostPerHeadPerMonth: costPerHead,
            IsTierDisabled: isTierDisabled
        );
    }
}

