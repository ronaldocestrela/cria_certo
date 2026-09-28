using CriaCerto.BuildingBlocks.Abstractions.Results;

namespace CriaCerto.Modules.Tenancy.Application.Abstractions;

public sealed record SubscriptionLifecycleExecutionResult(
    int TotalEvaluated,
    int SuspendedTrials,
    int SuspendedPastDue,
    int ProtectedSkipped
);

public interface ISubscriptionLifecycleService
{
    Task<Result<SubscriptionLifecycleExecutionResult>> ExecutePassAsync(CancellationToken cancellationToken = default);
}
