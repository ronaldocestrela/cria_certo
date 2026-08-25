using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Domain;

namespace CriaCerto.Modules.Backoffice.Infrastructure.Jobs;

public sealed record PlanLimitEnforcementResult(
    int TotalEvaluated,
    int TotalCompliantResolved,
    int TotalBlockedOverCapacity
);

public interface IPlanLimitEnforcementJob
{
    Task<Result<PlanLimitEnforcementResult>> ExecuteEnforcementPassAsync(CancellationToken cancellationToken = default);
}

public sealed class PlanLimitEnforcementJob : IPlanLimitEnforcementJob
{
    private readonly Func<Task<List<TenantSubscription>>> _getActiveSubscriptionsLookup;
    private readonly Func<TenantSubscription, Task> _updateSubscription;

    public PlanLimitEnforcementJob(
        Func<Task<List<TenantSubscription>>> getActiveSubscriptionsLookup,
        Func<TenantSubscription, Task> updateSubscription)
    {
        _getActiveSubscriptionsLookup = getActiveSubscriptionsLookup;
        _updateSubscription = updateSubscription;
    }

    public async Task<Result<PlanLimitEnforcementResult>> ExecuteEnforcementPassAsync(CancellationToken cancellationToken = default)
    {
        var subscriptions = await _getActiveSubscriptionsLookup();
        int evaluated = 0;
        int resolved = 0;
        int blocked = 0;

        var now = DateTime.UtcNow;

        foreach (var sub in subscriptions)
        {
            if (sub.Status == SubscriptionStatus.GracePeriodActive && sub.GracePeriodEndsAtUtc.HasValue)
            {
                evaluated++;
                if (sub.GracePeriodEndsAtUtc.Value <= now)
                {
                    // Expired Grace Period
                    // Flag over-capacity block
                    sub.Status = SubscriptionStatus.PendingDowngrade;
                    await _updateSubscription(sub);
                    blocked++;
                }
            }
        }

        return Result.Success(new PlanLimitEnforcementResult(evaluated, resolved, blocked));
    }
}
