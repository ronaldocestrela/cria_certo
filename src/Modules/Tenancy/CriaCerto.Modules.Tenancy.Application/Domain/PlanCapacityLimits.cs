using System;
using System.Collections.Generic;
using System.Linq;

namespace CriaCerto.Modules.Tenancy.Application.Domain;

public static class HeadTierCapacityLimits
{
    public const string Tier500 = "TIER_500";
    public const string Tier1500 = "TIER_1500";
    public const string Tier3000 = "TIER_3000";
    public const string Tier5000Plus = "TIER_5000_PLUS";

    public const int Tier500MaxHeads = 500;
    public const int Tier1500MaxHeads = 1500;
    public const int Tier3000MaxHeads = 3000;
    public const int Tier5000PlusMaxHeads = 10000;

    public const string CoreModuleKey = "Cows";

    public static readonly IReadOnlyList<string> AllHeadTierCodes =
    [
        Tier500,
        Tier1500,
        Tier3000,
        Tier5000Plus
    ];

    public static readonly IReadOnlyList<string> AllCommercialModules =
    [
        "Cows",
        "Breeding",
        "Calving",
        "Growth",
        "Sanitary",
        "Nutrition",
        "Analytics"
    ];

    public static bool IsValidHeadTierCode(string? tierCode)
    {
        if (string.IsNullOrWhiteSpace(tierCode))
        {
            return false;
        }

        return AllHeadTierCodes.Contains(tierCode.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    public static string NormalizeHeadTierCode(string tierCode)
    {
        return tierCode.Trim().ToUpperInvariant();
    }

    public static string GetTierDisplayName(string? tierCode)
    {
        return tierCode?.Trim().ToUpperInvariant() switch
        {
            Tier500 => "Até 500 cabeças",
            Tier1500 => "501 a 1.500 cabeças",
            Tier3000 => "1.501 a 3.000 cabeças",
            Tier5000Plus => "Acima de 3.000 cabeças",
            _ => tierCode ?? string.Empty
        };
    }

    public static int GetMaxHeadsForTier(string? tierCode)
    {
        return tierCode?.Trim().ToUpperInvariant() switch
        {
            Tier500 => Tier500MaxHeads,
            Tier1500 => Tier1500MaxHeads,
            Tier3000 => Tier3000MaxHeads,
            Tier5000Plus => Tier5000PlusMaxHeads,
            _ => Tier500MaxHeads
        };
    }

    public static bool IsCapacityWithinTier(string? tierCode, int capacity)
    {
        if (capacity <= 0 || !IsValidHeadTierCode(tierCode))
        {
            return false;
        }

        return capacity <= GetMaxHeadsForTier(tierCode);
    }

    public static int ResolveTenantCapacityLimit(Tenant tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);

        if (IsValidHeadTierCode(tenant.HeadTierCode) &&
            !string.Equals(tenant.HeadTierCode, Tier500, StringComparison.OrdinalIgnoreCase))
        {
            return tenant.Capacity > 0
                ? tenant.Capacity
                : GetMaxHeadsForTier(tenant.HeadTierCode);
        }

        if (!string.IsNullOrWhiteSpace(tenant.SubscribedPlan) &&
            !string.Equals(tenant.SubscribedPlan, "Starter", StringComparison.OrdinalIgnoreCase) &&
            !IsValidHeadTierCode(tenant.SubscribedPlan))
        {
            return PlanCapacityLimits.GetHeadCapacityLimit(tenant.SubscribedPlan);
        }

        return tenant.Capacity > 0
            ? tenant.Capacity
            : GetMaxHeadsForTier(tenant.HeadTierCode);
    }

    public static bool IsWithinTenantCapacity(Tenant tenant, int headCount)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        return headCount >= 0 && headCount <= ResolveTenantCapacityLimit(tenant);
    }

    public static bool IsValidCommercialModuleKey(string? moduleKey)
    {
        if (string.IsNullOrWhiteSpace(moduleKey))
        {
            return false;
        }

        return AllCommercialModules.Contains(moduleKey.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    public static string? NormalizeCommercialModuleKey(string? moduleKey)
    {
        if (string.IsNullOrWhiteSpace(moduleKey))
        {
            return null;
        }

        return AllCommercialModules.FirstOrDefault(m =>
            string.Equals(m, moduleKey.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}

public static class PlanCapacityLimits
{
    public const int StarterLimit = 500;
    public const int ProLimit = 2500;

    public const string Tier500 = HeadTierCapacityLimits.Tier500;
    public const string Tier1500 = HeadTierCapacityLimits.Tier1500;
    public const string Tier3000 = HeadTierCapacityLimits.Tier3000;
    public const string Tier5000Plus = HeadTierCapacityLimits.Tier5000Plus;

    public const int Tier500MaxHeads = HeadTierCapacityLimits.Tier500MaxHeads;
    public const int Tier1500MaxHeads = HeadTierCapacityLimits.Tier1500MaxHeads;
    public const int Tier3000MaxHeads = HeadTierCapacityLimits.Tier3000MaxHeads;
    public const int Tier5000PlusMaxHeads = HeadTierCapacityLimits.Tier5000PlusMaxHeads;

    public const string CoreModuleKey = HeadTierCapacityLimits.CoreModuleKey;

    public static readonly IReadOnlyList<string> AllHeadTierCodes = HeadTierCapacityLimits.AllHeadTierCodes;
    public static readonly IReadOnlyList<string> AllCommercialModules = HeadTierCapacityLimits.AllCommercialModules;

    public static bool IsValidHeadTierCode(string? tierCode) =>
        HeadTierCapacityLimits.IsValidHeadTierCode(tierCode);

    public static string NormalizeHeadTierCode(string tierCode) =>
        HeadTierCapacityLimits.NormalizeHeadTierCode(tierCode);

    public static string GetTierDisplayName(string? tierCode) =>
        HeadTierCapacityLimits.GetTierDisplayName(tierCode);

    public static int GetMaxHeadsForTier(string? tierCode) =>
        HeadTierCapacityLimits.GetMaxHeadsForTier(tierCode);

    public static bool IsValidCommercialModuleKey(string? moduleKey) =>
        HeadTierCapacityLimits.IsValidCommercialModuleKey(moduleKey);

    public static string? NormalizeCommercialModuleKey(string? moduleKey) =>
        HeadTierCapacityLimits.NormalizeCommercialModuleKey(moduleKey);

    public static int GetHeadCapacityLimit(string? plan)
    {
        return plan?.Trim().ToUpperInvariant() switch
        {
            "STARTER" => StarterLimit,
            "PRO" => ProLimit,
            Tier500 => Tier500MaxHeads,
            Tier1500 => Tier1500MaxHeads,
            Tier3000 => Tier3000MaxHeads,
            Tier5000Plus => Tier5000PlusMaxHeads,
            _ => int.MaxValue
        };
    }

    public static bool IsCapacityWithinPlan(string? plan, int capacity)
    {
        return capacity <= GetHeadCapacityLimit(plan);
    }
}
