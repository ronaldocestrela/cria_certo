using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Abstractions;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Domain.Errors;
using CriaCerto.Modules.Referral.Application.Features.PartnerPortal.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Referral.Application.Features.PartnerPortal.Queries;

public record GetPartnerCommissionStatementQuery(
    Guid UserId,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    LedgerRewardStatus? Status = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 10
) : IRequest<Result<PartnerCommissionStatementDto>>;

public sealed class GetPartnerCommissionStatementQueryHandler
    : IRequestHandler<GetPartnerCommissionStatementQuery, Result<PartnerCommissionStatementDto>>
{
    private readonly IReferralDbContext _dbContext;
    private readonly ITenantInfoLookupService? _tenantInfoLookupService;

    public GetPartnerCommissionStatementQueryHandler(
        IReferralDbContext dbContext,
        ITenantInfoLookupService? tenantInfoLookupService = null)
    {
        _dbContext = dbContext;
        _tenantInfoLookupService = tenantInfoLookupService;
    }

    public async Task<Result<PartnerCommissionStatementDto>> Handle(
        GetPartnerCommissionStatementQuery request,
        CancellationToken cancellationToken)
    {
        var participant = await _dbContext.Participants
            .FirstOrDefaultAsync(p => p.UserId == request.UserId && p.ParticipantType == ParticipantType.CommercialPartner, cancellationToken);

        if (participant == null)
        {
            return Result.Failure<PartnerCommissionStatementDto>(ReferralErrors.Participant.NotFound);
        }

        var contract = await _dbContext.PartnerContracts
            .FirstOrDefaultAsync(c => c.ParticipantId == participant.Id && c.IsActive, cancellationToken);

        var rateDescription = contract != null
            ? contract.CommissionType switch
            {
                CommissionType.PercentageRecurring => $"{contract.CommissionValue:N0}% Recorrente",
                CommissionType.PercentageFirstMonth => $"{contract.CommissionValue:N0}% 1º Mês",
                CommissionType.FixedPerConversion => $"R$ {contract.CommissionValue:N2} Fixo",
                _ => $"{contract.CommissionValue:N0}%"
            }
            : "Comissão Comercial";

        var query = _dbContext.RewardLedger
            .Where(l => l.ParticipantId == participant.Id && l.RewardType == RewardType.CashCommission)
            .AsQueryable();

        if (request.StartDate.HasValue)
        {
            query = query.Where(l => l.CreatedAtUtc >= request.StartDate.Value);
        }

        if (request.EndDate.HasValue)
        {
            query = query.Where(l => l.CreatedAtUtc <= request.EndDate.Value);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(l => l.Status == request.Status.Value);
        }

        var allMatchingLedgers = await query
            .OrderByDescending(l => l.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var attributionIds = allMatchingLedgers.Select(l => l.ReferralAttributionId).Distinct().ToList();
        var attributions = await _dbContext.Attributions
            .Where(a => attributionIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        var tenantIds = attributions.Values.Select(a => a.ReferredTenantId).Distinct().ToList();
        var tenantSummaries = _tenantInfoLookupService != null && tenantIds.Count > 0
            ? await _tenantInfoLookupService.GetTenantSummariesAsync(tenantIds, cancellationToken)
            : new Dictionary<Guid, TenantSummaryDto>();

        var statementItems = new List<PartnerStatementItemDto>();

        foreach (var ledger in allMatchingLedgers)
        {
            attributions.TryGetValue(ledger.ReferralAttributionId, out var attribution);
            var tenantId = attribution?.ReferredTenantId ?? Guid.Empty;

            var tenantName = "Fazenda Indicada";
            if (tenantSummaries.TryGetValue(tenantId, out var summary))
            {
                tenantName = !string.IsNullOrWhiteSpace(summary.TradeName)
                    ? summary.TradeName
                    : summary.Name;
            }

            // Filtro textual por nome de fazenda ou código de comprovante/transação
            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var term = request.Search.Trim().ToLowerInvariant();
                var matchesName = tenantName.ToLowerInvariant().Contains(term);
                var matchesRef = ledger.PayoutTransactionReference?.ToLowerInvariant().Contains(term) ?? false;
                var matchesInvoice = ledger.StripeInvoiceId?.ToLowerInvariant().Contains(term) ?? false;

                if (!matchesName && !matchesRef && !matchesInvoice)
                {
                    continue;
                }
            }

            statementItems.Add(new PartnerStatementItemDto(
                ledger.Id,
                ledger.CreatedAtUtc,
                tenantId,
                tenantName,
                ledger.GrossAmount,
                rateDescription,
                ledger.NetAmount,
                ledger.Currency,
                ledger.Status,
                ledger.MaturityDateUtc,
                ledger.PayoutTransactionReference,
                ledger.PayoutReceiptFilePath,
                ledger.PayoutRegisteredAtUtc
            ));
        }

        var page = request.Page <= 0 ? 1 : request.Page;
        var pageSize = request.PageSize <= 0 ? 10 : request.PageSize;
        var totalCount = statementItems.Count;

        var pagedItems = statementItems
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var resultDto = new PartnerCommissionStatementDto(pagedItems, totalCount, page, pageSize);
        return Result.Success(resultDto);
    }
}
