using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Abstractions;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Domain.Errors;
using CriaCerto.Modules.Referral.Application.Features.PartnerPortal.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Referral.Application.Features.PartnerPortal.Queries;

public record GetPartnerPortalDashboardQuery(Guid UserId)
    : IRequest<Result<PartnerPortalDashboardDto>>;

public sealed class GetPartnerPortalDashboardQueryHandler
    : IRequestHandler<GetPartnerPortalDashboardQuery, Result<PartnerPortalDashboardDto>>
{
    private readonly IReferralDbContext _dbContext;

    public GetPartnerPortalDashboardQueryHandler(IReferralDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<PartnerPortalDashboardDto>> Handle(
        GetPartnerPortalDashboardQuery request,
        CancellationToken cancellationToken)
    {
        var participant = await _dbContext.Participants
            .FirstOrDefaultAsync(p => p.UserId == request.UserId && p.ParticipantType == ParticipantType.CommercialPartner, cancellationToken);

        if (participant == null)
        {
            return Result.Failure<PartnerPortalDashboardDto>(ReferralErrors.Participant.NotFound);
        }

        var profile = await _dbContext.PartnerProfiles
            .FirstOrDefaultAsync(p => p.ParticipantId == participant.Id, cancellationToken);

        if (profile == null)
        {
            return Result.Failure<PartnerPortalDashboardDto>(ReferralErrors.PartnerProfile.NotFound);
        }

        var contract = await _dbContext.PartnerContracts
            .FirstOrDefaultAsync(c => c.ParticipantId == participant.Id && c.IsActive, cancellationToken);

        if (contract == null)
        {
            return Result.Failure<PartnerPortalDashboardDto>(ReferralErrors.Contract.NotFound);
        }

        // Campanhas e Códigos
        var codes = await _dbContext.ReferralCodes
            .Where(c => c.ParticipantId == participant.Id)
            .OrderBy(c => c.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var totalClicks = codes.Sum(c => c.ClicksCount);
        var mainCode = codes.FirstOrDefault()?.Code ?? string.Empty;

        var campaignsList = codes.Select(c => new PartnerCampaignSummaryDto(
            c.Id,
            c.Code,
            c.CampaignTitle,
            c.ClicksCount,
            c.IsActive,
            c.CreatedAtUtc
        )).ToList();

        // Atribuições (Trial vs Pagas)
        var attributions = await _dbContext.Attributions
            .Where(a => a.ReferrerParticipantId == participant.Id)
            .ToListAsync(cancellationToken);

        var trialCount = attributions.Count(a => a.Status == AttributionStatus.Pending);
        var paidCount = attributions.Count(a => a.Status == AttributionStatus.Converted);
        var totalSignups = trialCount + paidCount;
        var conversionRate = totalSignups > 0
            ? Math.Round((decimal)paidCount / totalSignups * 100m, 1)
            : 0m;

        // Saldos Financeiros
        var ledgerEntries = await _dbContext.RewardLedger
            .Where(l => l.ParticipantId == participant.Id && l.RewardType == RewardType.CashCommission)
            .ToListAsync(cancellationToken);

        var pendingMaturity = ledgerEntries
            .Where(l => l.Status == LedgerRewardStatus.PendingMaturity)
            .Sum(l => l.NetAmount);

        var availablePayout = ledgerEntries
            .Where(l => l.Status == LedgerRewardStatus.AvailableForPayout)
            .Sum(l => l.NetAmount);

        var totalPaid = ledgerEntries
            .Where(l => l.Status == LedgerRewardStatus.PaidOut)
            .Sum(l => l.NetAmount);

        var dto = new PartnerPortalDashboardDto(
            participant.Id,
            profile.FullName,
            profile.TradeName,
            profile.Email,
            profile.PhoneNumber,
            profile.Category,
            contract.LegalDocumentNumber,
            mainCode,
            totalClicks,
            trialCount,
            paidCount,
            conversionRate,
            pendingMaturity,
            availablePayout,
            totalPaid,
            "BRL",
            contract.CommissionType,
            contract.CommissionValue,
            contract.MaxRecurringMonths,
            contract.PaymentTermsDays,
            contract.PixKeyType,
            contract.PixKey,
            campaignsList
        );

        return Result.Success(dto);
    }
}
