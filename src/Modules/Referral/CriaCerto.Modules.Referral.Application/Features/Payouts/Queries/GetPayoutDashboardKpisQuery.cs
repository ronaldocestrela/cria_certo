using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Abstractions;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Features.Payouts.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Referral.Application.Features.Payouts.Queries;

public sealed record GetPayoutDashboardKpisQuery : IRequest<Result<PayoutDashboardKpiDto>>;

public sealed class GetPayoutDashboardKpisQueryHandler
    : IRequestHandler<GetPayoutDashboardKpisQuery, Result<PayoutDashboardKpiDto>>
{
    private readonly IReferralDbContext _dbContext;

    public GetPayoutDashboardKpisQueryHandler(IReferralDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<PayoutDashboardKpiDto>> Handle(
        GetPayoutDashboardKpisQuery request,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var pendingCommissions = await _dbContext.RewardLedger
            .AsNoTracking()
            .Where(l => l.Status == LedgerRewardStatus.PendingMaturity && l.RewardType == RewardType.CashCommission)
            .Select(l => l.NetAmount)
            .ToListAsync(cancellationToken);

        var totalPendingAmount = pendingCommissions.Sum();
        var totalPendingCount = pendingCommissions.Count;

        var availableCommissions = await _dbContext.RewardLedger
            .AsNoTracking()
            .Where(l => l.Status == LedgerRewardStatus.AvailableForPayout && l.RewardType == RewardType.CashCommission)
            .Select(l => new { l.ParticipantId, l.NetAmount })
            .ToListAsync(cancellationToken);

        var totalAvailableAmount = availableCommissions.Sum(c => c.NetAmount);
        var totalAvailablePartnersCount = availableCommissions.Select(c => c.ParticipantId).Distinct().Count();

        var paidOutThisMonth = await _dbContext.RewardLedger
            .AsNoTracking()
            .Where(l => l.Status == LedgerRewardStatus.PaidOut &&
                        l.RewardType == RewardType.CashCommission &&
                        l.PayoutRegisteredAtUtc >= startOfMonth)
            .Select(l => l.NetAmount)
            .ToListAsync(cancellationToken);

        var totalPaidOutMonthAmount = paidOutThisMonth.Sum();
        var totalPaidOutMonthCount = paidOutThisMonth.Count;

        var dto = new PayoutDashboardKpiDto(
            TotalPendingMaturityAmount: totalPendingAmount,
            TotalPendingMaturityCount: totalPendingCount,
            TotalAvailableForPayoutAmount: totalAvailableAmount,
            TotalAvailablePartnersCount: totalAvailablePartnersCount,
            TotalPaidOutThisMonthAmount: totalPaidOutMonthAmount,
            TotalPaidOutThisMonthCount: totalPaidOutMonthCount
        );

        return Result.Success(dto);
    }
}
