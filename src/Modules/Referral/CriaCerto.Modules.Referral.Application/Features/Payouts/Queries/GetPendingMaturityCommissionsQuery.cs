using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Abstractions;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Features.Payouts.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Referral.Application.Features.Payouts.Queries;

public sealed record GetPendingMaturityCommissionsQuery(
    string? SearchTerm = null
) : IRequest<Result<IReadOnlyList<PendingMaturityCommissionDto>>>;

public sealed class GetPendingMaturityCommissionsQueryHandler
    : IRequestHandler<GetPendingMaturityCommissionsQuery, Result<IReadOnlyList<PendingMaturityCommissionDto>>>
{
    private readonly IReferralDbContext _dbContext;

    public GetPendingMaturityCommissionsQueryHandler(IReferralDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<IReadOnlyList<PendingMaturityCommissionDto>>> Handle(
        GetPendingMaturityCommissionsQuery request,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var ledgersQuery = _dbContext.RewardLedger
            .AsNoTracking()
            .Where(l => l.Status == LedgerRewardStatus.PendingMaturity && l.RewardType == RewardType.CashCommission);

        var ledgers = await ledgersQuery
            .OrderBy(l => l.MaturityDateUtc)
            .ToListAsync(cancellationToken);

        if (ledgers.Count == 0)
        {
            return Result.Success<IReadOnlyList<PendingMaturityCommissionDto>>(Array.Empty<PendingMaturityCommissionDto>());
        }

        var participantIds = ledgers.Select(l => l.ParticipantId).Distinct().ToList();
        var attributionIds = ledgers.Select(l => l.ReferralAttributionId).Distinct().ToList();

        var profiles = await _dbContext.PartnerProfiles
            .AsNoTracking()
            .Where(p => participantIds.Contains(p.ParticipantId))
            .ToDictionaryAsync(p => p.ParticipantId, cancellationToken);

        var contracts = await _dbContext.PartnerContracts
            .AsNoTracking()
            .Where(c => participantIds.Contains(c.ParticipantId) && c.IsActive)
            .ToDictionaryAsync(c => c.ParticipantId, cancellationToken);

        var attributions = await _dbContext.Attributions
            .AsNoTracking()
            .Where(a => attributionIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        var codeIds = attributions.Values.Select(a => a.ReferralCodeId).Distinct().ToList();
        var codes = await _dbContext.ReferralCodes
            .AsNoTracking()
            .Where(c => codeIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        var dtos = new List<PendingMaturityCommissionDto>(ledgers.Count);

        foreach (var l in ledgers)
        {
            profiles.TryGetValue(l.ParticipantId, out var profile);
            contracts.TryGetValue(l.ParticipantId, out var contract);
            attributions.TryGetValue(l.ReferralAttributionId, out var attr);

            string? refCode = null;
            if (attr != null && codes.TryGetValue(attr.ReferralCodeId, out var codeObj))
            {
                refCode = codeObj.Code;
            }

            var partnerName = profile?.FullName ?? "Parceiro Comercial";
            var tradeName = profile?.TradeName;
            var docNumber = contract?.LegalDocumentNumber ?? string.Empty;

            var remainingDays = 0;
            if (l.MaturityDateUtc.HasValue && l.MaturityDateUtc.Value > now)
            {
                remainingDays = (int)Math.Ceiling((l.MaturityDateUtc.Value - now).TotalDays);
            }

            dtos.Add(new PendingMaturityCommissionDto(
                LedgerId: l.Id,
                ParticipantId: l.ParticipantId,
                PartnerName: partnerName,
                TradeName: tradeName,
                LegalDocumentNumber: docNumber,
                NetAmount: l.NetAmount,
                Currency: l.Currency,
                CreatedAtUtc: l.CreatedAtUtc,
                MaturityDateUtc: l.MaturityDateUtc,
                RemainingDays: remainingDays,
                StripeInvoiceId: l.StripeInvoiceId,
                ReferralCode: refCode
            ));
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLowerInvariant();
            dtos = dtos.Where(d =>
                d.PartnerName.ToLowerInvariant().Contains(term) ||
                (d.TradeName != null && d.TradeName.ToLowerInvariant().Contains(term)) ||
                d.LegalDocumentNumber.Contains(term) ||
                (d.ReferralCode != null && d.ReferralCode.ToLowerInvariant().Contains(term))
            ).ToList();
        }

        return Result.Success<IReadOnlyList<PendingMaturityCommissionDto>>(dtos);
    }
}
