using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Domain.Errors;

namespace CriaCerto.Modules.Tenancy.Application.Domain;

public enum SubscriptionStatus
{
    Active = 1,
    GracePeriodActive = 2,
    PendingDowngrade = 3,
    Cancelled = 4
}

public enum SubscriptionActionType
{
    Upgrade = 1,
    DowngradeImmediate = 2,
    DowngradeGracePeriodStarted = 3,
    GracePeriodResolved = 4,
    GracePeriodExpiredBlocked = 5,
    NewSubscription = 6,
    Renewal = 7,
    PaymentFailed = 8,
    Cancelled = 9,
    PlanChanged = 10,
    Suspended = 11
}


public sealed class TenantSubscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid PlanCatalogId { get; set; }
    public Guid PlanVersionId { get; set; }
    public string PlanCode { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public string HeadTierCode { get; set; } = PlanCapacityLimits.Tier500;
    public string? PendingHeadTierCode { get; set; }
    public List<string> SubscribedModuleKeys { get; set; } = [PlanCapacityLimits.CoreModuleKey];
    public int VersionNumber { get; set; }
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;

    public int MaxHeadCapacity { get; set; }
    public int MaxUsers { get; set; }
    public int MaxProductionUnits { get; set; }

    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? GracePeriodStartedAtUtc { get; set; }
    public DateTime? GracePeriodEndsAtUtc { get; set; }
    public Guid? PendingPlanVersionId { get; set; }

    public static Result<TenantSubscription> CreateInitialModular(
        Guid tenantId,
        string headTierCode,
        int maxHeadCapacity,
        IEnumerable<string>? moduleKeys = null,
        int maxUsers = 10,
        int maxProductionUnits = 5)
    {
        if (!PlanCapacityLimits.IsValidHeadTierCode(headTierCode))
        {
            return Result.Failure<TenantSubscription>(TenancyErrors.InvalidHeadTierCode);
        }

        var normalizedModules = NormalizeAndEnsureCoreModules(moduleKeys);
        if (normalizedModules.IsFailure)
        {
            return Result.Failure<TenantSubscription>(normalizedModules.Error);
        }

        var normalizedTier = PlanCapacityLimits.NormalizeHeadTierCode(headTierCode);
        return Result.Success(new TenantSubscription
        {
            TenantId = tenantId,
            HeadTierCode = normalizedTier,
            PlanCode = normalizedTier,
            PlanName = normalizedTier,
            VersionNumber = 1,
            Status = SubscriptionStatus.Active,
            MaxHeadCapacity = maxHeadCapacity > 0 ? maxHeadCapacity : PlanCapacityLimits.GetMaxHeadsForTier(normalizedTier),
            MaxUsers = maxUsers,
            MaxProductionUnits = maxProductionUnits,
            SubscribedModuleKeys = normalizedModules.Value,
            StartedAtUtc = DateTime.UtcNow
        });
    }

    public Result ApplyModularChange(
        string targetHeadTierCode,
        int targetMaxHeadCapacity,
        IEnumerable<string> targetModuleKeys,
        int currentHeadCount,
        bool forceImmediate = false)
    {
        if (!PlanCapacityLimits.IsValidHeadTierCode(targetHeadTierCode))
        {
            return Result.Failure(TenancyErrors.InvalidHeadTierCode);
        }

        var normalizedModulesResult = NormalizeAndEnsureCoreModules(targetModuleKeys);
        if (normalizedModulesResult.IsFailure)
        {
            return Result.Failure(normalizedModulesResult.Error);
        }

        var normalizedTier = PlanCapacityLimits.NormalizeHeadTierCode(targetHeadTierCode);
        var effectiveTargetCapacity = targetMaxHeadCapacity > 0
            ? targetMaxHeadCapacity
            : PlanCapacityLimits.GetMaxHeadsForTier(normalizedTier);

        if (currentHeadCount > effectiveTargetCapacity && !forceImmediate)
        {
            Status = SubscriptionStatus.GracePeriodActive;
            GracePeriodStartedAtUtc = DateTime.UtcNow;
            GracePeriodEndsAtUtc = DateTime.UtcNow.AddDays(14);
            PendingHeadTierCode = normalizedTier;
            return Result.Success();
        }

        HeadTierCode = normalizedTier;
        PlanCode = normalizedTier;
        PlanName = normalizedTier;
        MaxHeadCapacity = effectiveTargetCapacity;
        SubscribedModuleKeys = normalizedModulesResult.Value;
        Status = SubscriptionStatus.Active;
        GracePeriodStartedAtUtc = null;
        GracePeriodEndsAtUtc = null;
        PendingHeadTierCode = null;
        PendingPlanVersionId = null;

        return Result.Success();
    }

    public Result ResolveModularGracePeriod(int targetMaxHeadCapacity, int currentHeadCount)
    {
        if (Status != SubscriptionStatus.GracePeriodActive || string.IsNullOrWhiteSpace(PendingHeadTierCode))
        {
            return Result.Success();
        }

        var effectiveTargetCapacity = targetMaxHeadCapacity > 0
            ? targetMaxHeadCapacity
            : PlanCapacityLimits.GetMaxHeadsForTier(PendingHeadTierCode);

        if (currentHeadCount <= effectiveTargetCapacity)
        {
            HeadTierCode = PendingHeadTierCode;
            PlanCode = PendingHeadTierCode;
            PlanName = PendingHeadTierCode;
            MaxHeadCapacity = effectiveTargetCapacity;
            Status = SubscriptionStatus.Active;
            GracePeriodStartedAtUtc = null;
            GracePeriodEndsAtUtc = null;
            PendingHeadTierCode = null;
            PendingPlanVersionId = null;
        }

        return Result.Success();
    }

    private static Result<List<string>> NormalizeAndEnsureCoreModules(IEnumerable<string>? moduleKeys)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            PlanCapacityLimits.CoreModuleKey
        };

        if (moduleKeys is not null)
        {
            foreach (var raw in moduleKeys)
            {
                var normalized = PlanCapacityLimits.NormalizeCommercialModuleKey(raw);
                if (normalized is null)
                {
                    return Result.Failure<List<string>>(TenancyErrors.InvalidModuleKey);
                }

                set.Add(normalized);
            }
        }

        var ordered = PlanCapacityLimits.AllCommercialModules
            .Where(m => set.Contains(m))
            .ToList();

        return Result.Success(ordered);
    }

    public static TenantSubscription CreateInitial(
        Guid tenantId,
        Guid planCatalogId,
        Guid planVersionId,
        string planCode,
        string planName,
        int versionNumber,
        int maxHeadCapacity,
        int maxUsers = 10,
        int maxProductionUnits = 5)
    {
        return new TenantSubscription
        {
            TenantId = tenantId,
            PlanCatalogId = planCatalogId,
            PlanVersionId = planVersionId,
            PlanCode = planCode,
            PlanName = planName,
            VersionNumber = versionNumber,
            Status = SubscriptionStatus.Active,
            MaxHeadCapacity = maxHeadCapacity,
            MaxUsers = maxUsers,
            MaxProductionUnits = maxProductionUnits,
            StartedAtUtc = DateTime.UtcNow
        };
    }

    public Result ApplyPlanChange(
        Guid targetPlanCatalogId,
        Guid targetPlanVersionId,
        string targetPlanCode,
        string targetPlanName,
        int targetVersionNumber,
        int targetMaxHeadCapacity,
        int targetMaxUsers,
        int targetMaxProductionUnits,
        int currentHeadCount,
        int currentActiveUsers,
        int currentProductionUnits,
        bool forceImmediate = false)
    {
        if (targetPlanVersionId == PlanVersionId)
        {
            return Result.Failure(TenancyErrors.AlreadySubscribedToPlanVersion);
        }

        bool exceedsUsage = currentHeadCount > targetMaxHeadCapacity ||
                            currentActiveUsers > targetMaxUsers ||
                            currentProductionUnits > targetMaxProductionUnits;

        if (exceedsUsage && !forceImmediate)
        {
            // Entra em Grace Period de 14 dias
            Status = SubscriptionStatus.GracePeriodActive;
            GracePeriodStartedAtUtc = DateTime.UtcNow;
            GracePeriodEndsAtUtc = DateTime.UtcNow.AddDays(14);
            PendingPlanVersionId = targetPlanVersionId;
            return Result.Success();
        }

        // Aplicação Imediata (Upgrade ou Downgrade dentro dos limites / Forçado)
        PlanCatalogId = targetPlanCatalogId;
        PlanVersionId = targetPlanVersionId;
        PlanCode = targetPlanCode;
        PlanName = targetPlanName;
        VersionNumber = targetVersionNumber;
        MaxHeadCapacity = targetMaxHeadCapacity;
        MaxUsers = targetMaxUsers;
        MaxProductionUnits = targetMaxProductionUnits;
        Status = SubscriptionStatus.Active;
        GracePeriodStartedAtUtc = null;
        GracePeriodEndsAtUtc = null;
        PendingPlanVersionId = null;

        return Result.Success();
    }

    public Result ResolveGracePeriod(
        Guid targetPlanCatalogId,
        Guid targetPlanVersionId,
        string targetPlanCode,
        string targetPlanName,
        int targetVersionNumber,
        int targetMaxHeadCapacity,
        int targetMaxUsers,
        int targetMaxProductionUnits,
        int currentHeadCount,
        int currentActiveUsers,
        int currentProductionUnits)
    {
        if (Status != SubscriptionStatus.GracePeriodActive)
        {
            return Result.Success();
        }

        bool supportsUsage = currentHeadCount <= targetMaxHeadCapacity &&
                             currentActiveUsers <= targetMaxUsers &&
                             currentProductionUnits <= targetMaxProductionUnits;

        if (supportsUsage)
        {
            PlanCatalogId = targetPlanCatalogId;
            PlanVersionId = targetPlanVersionId;
            PlanCode = targetPlanCode;
            PlanName = targetPlanName;
            VersionNumber = targetVersionNumber;
            MaxHeadCapacity = targetMaxHeadCapacity;
            MaxUsers = targetMaxUsers;
            MaxProductionUnits = targetMaxProductionUnits;
            Status = SubscriptionStatus.Active;
            GracePeriodStartedAtUtc = null;
            GracePeriodEndsAtUtc = null;
            PendingPlanVersionId = null;
        }

        return Result.Success();
    }
}
