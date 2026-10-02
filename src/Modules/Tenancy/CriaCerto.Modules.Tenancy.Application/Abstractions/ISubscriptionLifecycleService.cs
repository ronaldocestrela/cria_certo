using CriaCerto.BuildingBlocks.Abstractions.Results;

namespace CriaCerto.Modules.Tenancy.Application.Abstractions;

public sealed record SubscriptionLifecycleExecutionResult(
    int TotalEvaluated,
    int SuspendedTrials,
    int SuspendedPastDue,
    int ProtectedSkipped,
    int SuspendedActive = 0,
    int TransitionedToPastDue = 0
);

public interface ISubscriptionLifecycleService
{
    Task<Result<SubscriptionLifecycleExecutionResult>> ExecutePassAsync(CancellationToken cancellationToken = default);
}
