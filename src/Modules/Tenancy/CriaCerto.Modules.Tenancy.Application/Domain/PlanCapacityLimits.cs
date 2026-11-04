namespace CriaCerto.Modules.Tenancy.Application.Domain;

public static class PlanCapacityLimits
{
    public const int StarterLimit = 500;
    public const int ProLimit = 2500;

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
