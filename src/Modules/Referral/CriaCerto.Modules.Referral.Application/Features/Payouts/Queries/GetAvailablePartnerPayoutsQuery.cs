using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Abstractions;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Features.Payouts.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Referral.Application.Features.Payouts.Queries;

public sealed record GetAvailablePartnerPayoutsQuery(
    string? SearchTerm = null,
    IReadOnlyList<Guid>? FilterParticipantIds = null
) : IRequest<Result<IReadOnlyList<AvailablePartnerPayoutDto>>>;

public sealed class GetAvailablePartnerPayoutsQueryHandler
    : IRequestHandler<GetAvailablePartnerPayoutsQuery, Result<IReadOnlyList<AvailablePartnerPayoutDto>>>
{
    private readonly IReferralDbContext _dbContext;

    public GetAvailablePartnerPayoutsQueryHandler(IReferralDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<IReadOnlyList<AvailablePartnerPayoutDto>>> Handle(
        GetAvailablePartnerPayoutsQuery request,
        CancellationToken cancellationToken)
    {
        var ledgersQuery = _dbContext.RewardLedger
            .AsNoTracking()
            .Where(l => l.Status == LedgerRewardStatus.AvailableForPayout && l.RewardType == RewardType.CashCommission);

        if (request.FilterParticipantIds != null && request.FilterParticipantIds.Count > 0)
        {
            ledgersQuery = ledgersQuery.Where(l => request.FilterParticipantIds.Contains(l.ParticipantId));
        }

        var availableLedgers = await ledgersQuery
            .OrderBy(l => l.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        if (availableLedgers.Count == 0)
        {
            return Result.Success<IReadOnlyList<AvailablePartnerPayoutDto>>(Array.Empty<AvailablePartnerPayoutDto>());
        }

        var grouped = availableLedgers
            .GroupBy(l => l.ParticipantId)
            .ToList();

        var participantIds = grouped.Select(g => g.Key).ToList();

        var profiles = await _dbContext.PartnerProfiles
            .AsNoTracking()
            .Where(p => participantIds.Contains(p.ParticipantId))
            .ToDictionaryAsync(p => p.ParticipantId, cancellationToken);

        var contracts = await _dbContext.PartnerContracts
            .AsNoTracking()
            .Where(c => participantIds.Contains(c.ParticipantId) && c.IsActive)
            .ToDictionaryAsync(c => c.ParticipantId, cancellationToken);

        var resultList = new List<AvailablePartnerPayoutDto>(grouped.Count);

        foreach (var group in grouped)
        {
            var pId = group.Key;
            profiles.TryGetValue(pId, out var profile);
            contracts.TryGetValue(pId, out var contract);

            var partnerName = profile?.FullName ?? "Parceiro Comercial";
            var tradeName = profile?.TradeName;
            var legalDoc = contract?.LegalDocumentNumber ?? string.Empty;
            var pixKey = contract?.PixKey ?? string.Empty;
            var pixKeyType = contract?.PixKeyType ?? PixKeyType.RandomKey;

            var commissions = group.Select(l => new CommissionDetailDto(
                LedgerId: l.Id,
                NetAmount: l.NetAmount,
                Currency: l.Currency,
                CreatedAtUtc: l.CreatedAtUtc,
                MaturityDateUtc: l.MaturityDateUtc,
                StripeInvoiceId: l.StripeInvoiceId,
                ReferralAttributionId: l.ReferralAttributionId
            )).ToList();

            var totalAmount = commissions.Sum(c => c.NetAmount);

            resultList.Add(new AvailablePartnerPayoutDto(
                ParticipantId: pId,
                PartnerName: partnerName,
                TradeName: tradeName,
                LegalDocumentNumber: legalDoc,
                PixKey: pixKey,
                PixKeyType: pixKeyType,
                TotalAvailableAmount: totalAmount,
                CommissionsCount: commissions.Count,
                Commissions: commissions
            ));
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLowerInvariant();
            resultList = resultList.Where(p =>
                p.PartnerName.ToLowerInvariant().Contains(term) ||
                (p.TradeName != null && p.TradeName.ToLowerInvariant().Contains(term)) ||
                p.LegalDocumentNumber.Contains(term) ||
                p.PixKey.ToLowerInvariant().Contains(term)
            ).ToList();
        }

        return Result.Success<IReadOnlyList<AvailablePartnerPayoutDto>>(resultList.OrderByDescending(p => p.TotalAvailableAmount).ToList());
    }
}
