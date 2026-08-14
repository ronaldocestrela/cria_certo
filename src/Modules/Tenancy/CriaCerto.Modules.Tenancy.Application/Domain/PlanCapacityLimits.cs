namespace CriaCerto.Modules.Tenancy.Application.Domain;

public static class PlanCapacityLimits
{
    public const int StarterLimit = 500;
    public const int ProLimit = 2500;

    public static int GetHeadCapacityLimit(string? plan)
    {
        return plan?.Trim().ToUpperInvariant() switch
        {
            "STARTER" => StarterLimit,
            "PRO" => ProLimit,
            _ => int.MaxValue
        };
    }

    public static bool IsCapacityWithinPlan(string? plan, int capacity)
    {
        return capacity <= GetHeadCapacityLimit(plan);
    }
}
