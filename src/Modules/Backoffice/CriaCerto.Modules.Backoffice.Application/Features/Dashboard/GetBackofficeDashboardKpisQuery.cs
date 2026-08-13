using CriaCerto.BuildingBlocks.Abstractions.Results;
using MediatR;

namespace CriaCerto.Modules.Backoffice.Application.Features.Dashboard;

public record GetBackofficeDashboardKpisQuery : IRequest<Result<BackofficeDashboardKpisDto>>;

public class GetBackofficeDashboardKpisQueryHandler : IRequestHandler<GetBackofficeDashboardKpisQuery, Result<BackofficeDashboardKpisDto>>
{
    public Task<Result<BackofficeDashboardKpisDto>> Handle(GetBackofficeDashboardKpisQuery request, CancellationToken cancellationToken)
    {
        // Initial aggregated KPIs (will query Tenancy module metrics or DB views)
        var dto = new BackofficeDashboardKpisDto(
            TotalTenants: 12,
            ActiveTenants: 10,
            TrialTenants: 2,
            PastDueTenants: 0,
            SuspendedTenants: 0,
            DelinquencyRatePercentage: 0.0m,
            ActiveSubscriptionsCount: 10,
            MonthlyRecurringRevenue: 14900.00m,
            SystemHealthStatus: "Healthy",
            CalculatedAtUtc: DateTime.UtcNow
        );

        return Task.FromResult(Result.Success(dto));
    }
}
