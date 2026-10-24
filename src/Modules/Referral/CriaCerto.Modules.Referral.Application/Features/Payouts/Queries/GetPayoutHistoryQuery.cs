using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Abstractions;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Features.Payouts.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Referral.Application.Features.Payouts.Queries;

public sealed record GetPayoutHistoryQuery(
    string? SearchTerm = null,
    int Page = 1,
    int PageSize = 50
) : IRequest<Result<IReadOnlyList<PayoutHistoryDto>>>;

public sealed class GetPayoutHistoryQueryHandler
    : IRequestHandler<GetPayoutHistoryQuery, Result<IReadOnlyList<PayoutHistoryDto>>>
{
    private readonly IReferralDbContext _dbContext;

    public GetPayoutHistoryQueryHandler(IReferralDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<IReadOnlyList<PayoutHistoryDto>>> Handle(
        GetPayoutHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 200 ? 50 : request.PageSize;

        var ledgersQuery = _dbContext.RewardLedger
            .AsNoTracking()
            .Where(l => l.Status == LedgerRewardStatus.PaidOut && l.RewardType == RewardType.CashCommission)
            .OrderByDescending(l => l.PayoutRegisteredAtUtc ?? l.UpdatedAtUtc ?? l.CreatedAtUtc);

        var ledgers = await ledgersQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        if (ledgers.Count == 0)
        {
            return Result.Success<IReadOnlyList<PayoutHistoryDto>>(Array.Empty<PayoutHistoryDto>());
        }

        var participantIds = ledgers.Select(l => l.ParticipantId).Distinct().ToList();

        var profiles = await _dbContext.PartnerProfiles
            .AsNoTracking()
            .Where(p => participantIds.Contains(p.ParticipantId))
            .ToDictionaryAsync(p => p.ParticipantId, cancellationToken);

        var contracts = await _dbContext.PartnerContracts
            .AsNoTracking()
            .Where(c => participantIds.Contains(c.ParticipantId))
            .ToDictionaryAsync(c => c.ParticipantId, cancellationToken);

        var dtos = ledgers.Select(l =>
        {
            profiles.TryGetValue(l.ParticipantId, out var profile);
            contracts.TryGetValue(l.ParticipantId, out var contract);

            var name = profile?.FullName ?? "Parceiro Comercial";
            var doc = contract?.LegalDocumentNumber ?? string.Empty;

            return new PayoutHistoryDto(
                LedgerId: l.Id,
                ParticipantId: l.ParticipantId,
                PartnerName: name,
                LegalDocumentNumber: doc,
                NetAmount: l.NetAmount,
                Currency: l.Currency,
                PayoutTransactionReference: l.PayoutTransactionReference,
                PayoutReceiptFilePath: l.PayoutReceiptFilePath,
                PayoutRegisteredAtUtc: l.PayoutRegisteredAtUtc,
                PayoutRegisteredByAdminId: l.PayoutRegisteredByAdminId,
                StripeInvoiceId: l.StripeInvoiceId
            );
        }).ToList();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLowerInvariant();
            dtos = dtos.Where(d =>
                d.PartnerName.ToLowerInvariant().Contains(term) ||
                d.LegalDocumentNumber.Contains(term) ||
                (d.PayoutTransactionReference != null && d.PayoutTransactionReference.ToLowerInvariant().Contains(term))
            ).ToList();
        }

        return Result.Success<IReadOnlyList<PayoutHistoryDto>>(dtos);
    }
}
