using System.Diagnostics;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Abstractions;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Features.Analytics.Dtos;
using CriaCerto.Modules.Referral.Application.Telemetry;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Referral.Application.Features.Analytics.Queries;

public record GetReferralProgramKpisQuery(
    DateTime? StartDateUtc = null,
    DateTime? EndDateUtc = null
) : IRequest<Result<ReferralProgramKpisDto>>;

public sealed class GetReferralProgramKpisQueryHandler : IRequestHandler<GetReferralProgramKpisQuery, Result<ReferralProgramKpisDto>>
{
    private readonly IReferralDbContext _dbContext;
    private readonly ITenantInfoLookupService _tenantLookupService;
    private readonly IPaidMediaSpendLookupService _paidMediaSpendLookupService;

    public GetReferralProgramKpisQueryHandler(
        IReferralDbContext dbContext,
        ITenantInfoLookupService tenantLookupService,
        IPaidMediaSpendLookupService paidMediaSpendLookupService)
    {
        _dbContext = dbContext;
        _tenantLookupService = tenantLookupService;
        _paidMediaSpendLookupService = paidMediaSpendLookupService;
    }

    public async Task<Result<ReferralProgramKpisDto>> Handle(GetReferralProgramKpisQuery request, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        // 1. Participantes e Códigos
        var participants = await _dbContext.Participants
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var participantMap = participants.ToDictionary(p => p.Id, p => p.ParticipantType);

        var codes = await _dbContext.ReferralCodes
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        long customerClicks = 0;
        long partnerClicks = 0;

        foreach (var code in codes)
        {
            if (participantMap.TryGetValue(code.ParticipantId, out var type))
            {
                if (type == ParticipantType.Customer) customerClicks += code.ClicksCount;
                else if (type == ParticipantType.CommercialPartner) partnerClicks += code.ClicksCount;
            }
        }
        long totalClicks = customerClicks + partnerClicks;

        // 2. Atribuições
        var attributionsQuery = _dbContext.Attributions.AsNoTracking();

        if (request.StartDateUtc.HasValue)
        {
            attributionsQuery = attributionsQuery.Where(a => a.AttributionDateUtc >= request.StartDateUtc.Value);
        }

        if (request.EndDateUtc.HasValue)
        {
            attributionsQuery = attributionsQuery.Where(a => a.AttributionDateUtc <= request.EndDateUtc.Value);
        }

        var attributions = await attributionsQuery.ToListAsync(cancellationToken);

        int totalAttributions = attributions.Count;
        int convertedAttributions = attributions.Count(a => a.Status == AttributionStatus.Converted);
        int pendingAttributions = attributions.Count(a => a.Status == AttributionStatus.Pending);
        int fraudulentAttributions = attributions.Count(a => a.Status == AttributionStatus.Fraudulent);
        int churnedAttributions = attributions.Count(a => a.Status == AttributionStatus.Churned);

        decimal clickToLeadRate = totalClicks > 0
            ? Math.Round((decimal)totalAttributions / totalClicks * 100m, 2)
            : 0m;

        decimal leadToPaidRate = totalAttributions > 0
            ? Math.Round((decimal)convertedAttributions / totalAttributions * 100m, 2)
            : 0m;

        decimal globalConversionRate = totalClicks > 0
            ? Math.Round((decimal)convertedAttributions / totalClicks * 100m, 2)
            : 0m;

        var funnelDto = new ReferralFunnelKpisDto(
            TotalClicks: totalClicks,
            CustomerClicks: customerClicks,
            PartnerClicks: partnerClicks,
            TotalAttributions: totalAttributions,
            ConvertedAttributions: convertedAttributions,
            PendingAttributions: pendingAttributions,
            FraudulentAttributions: fraudulentAttributions,
            ChurnedAttributions: churnedAttributions,
            ClickToLeadConversionRate: clickToLeadRate,
            LeadToPaidConversionRate: leadToPaidRate,
            GlobalConversionRate: globalConversionRate
        );

        // 3. Recompensas e CAC
        var rewardsQuery = _dbContext.RewardLedger.AsNoTracking();
        if (request.StartDateUtc.HasValue)
        {
            rewardsQuery = rewardsQuery.Where(r => r.CreatedAtUtc >= request.StartDateUtc.Value);
        }
        if (request.EndDateUtc.HasValue)
        {
            rewardsQuery = rewardsQuery.Where(r => r.CreatedAtUtc <= request.EndDateUtc.Value);
        }
        var rewards = await rewardsQuery.ToListAsync(cancellationToken);

        decimal customerTotalRewardSpent = 0m;
        decimal partnerTotalCommissionSpent = 0m;

        foreach (var reward in rewards.Where(r => r.Status != LedgerRewardStatus.Cancelled))
        {
            if (reward.RewardType == RewardType.InvoiceDiscountCredit)
            {
                customerTotalRewardSpent += reward.GrossAmount;
            }
            else if (reward.RewardType == RewardType.CashCommission)
            {
                partnerTotalCommissionSpent += reward.NetAmount;
            }
        }

        // Aquisições por canal de Referral
        int customerAcquisitions = 0;
        int partnerAcquisitions = 0;

        foreach (var conv in attributions.Where(a => a.Status == AttributionStatus.Converted))
        {
            if (participantMap.TryGetValue(conv.ReferrerParticipantId, out var type))
            {
                if (type == ParticipantType.Customer) customerAcquisitions++;
                else if (type == ParticipantType.CommercialPartner) partnerAcquisitions++;
            }
        }

        // Mídia Paga
        var paidMediaSummary = await _paidMediaSpendLookupService.GetPaidMediaSummaryAsync(
            request.StartDateUtc, request.EndDateUtc, cancellationToken);

        decimal customerCac = customerAcquisitions > 0
            ? Math.Round(customerTotalRewardSpent / customerAcquisitions, 2)
            : 0m;

        decimal partnerCac = partnerAcquisitions > 0
            ? Math.Round(partnerTotalCommissionSpent / partnerAcquisitions, 2)
            : 0m;

        decimal paidMediaCac = paidMediaSummary.TotalAcquisitions > 0
            ? Math.Round(paidMediaSummary.TotalSpent / paidMediaSummary.TotalAcquisitions, 2)
            : 0m;

        decimal totalSpentAllChannels = customerTotalRewardSpent + partnerTotalCommissionSpent + paidMediaSummary.TotalSpent;
        int totalAcquisitionsAllChannels = customerAcquisitions + partnerAcquisitions + paidMediaSummary.TotalAcquisitions;

        decimal blendedCac = totalAcquisitionsAllChannels > 0
            ? Math.Round(totalSpentAllChannels / totalAcquisitionsAllChannels, 2)
            : 0m;

        var cacDto = new ReferralCacKpisDto(
            CustomerCac: customerCac,
            PartnerCac: partnerCac,
            PaidMediaCac: paidMediaCac,
            BlendedCac: blendedCac,
            CustomerTotalRewardSpent: customerTotalRewardSpent,
            PartnerTotalCommissionSpent: partnerTotalCommissionSpent,
            PaidMediaTotalSpent: paidMediaSummary.TotalSpent,
            CustomerAcquisitionsCount: customerAcquisitions,
            PartnerAcquisitionsCount: partnerAcquisitions,
            PaidMediaAcquisitionsCount: paidMediaSummary.TotalAcquisitions
        );

        // 4. Viralidade (K-Factor)
        int activeTenantsCount = await _tenantLookupService.GetActiveTenantsCountAsync(cancellationToken);
        double kFactor = activeTenantsCount > 0
            ? Math.Round((double)customerAcquisitions / activeTenantsCount, 4)
            : 0.0;

        string viralStatus = kFactor >= 1.0 ? "Viral Expansivo" : "Subcrítico";

        var viralityDto = new ReferralViralityKpisDto(
            KFactor: kFactor,
            ViralStatus: viralStatus,
            ActivePlatformTenantsCount: activeTenantsCount,
            NewCustomersFromCustomerReferrals: customerAcquisitions
        );

        // 5. Retenção e LTV Comparativo
        var referredTenantIds = attributions
            .Where(a => a.Status == AttributionStatus.Converted)
            .Select(a => a.ReferredTenantId)
            .Distinct()
            .ToList();

        var retentionMetrics = await _tenantLookupService.GetTenantsRetentionMetricsAsync(
            referredTenantIds, cancellationToken);

        decimal lifespanAdvantage = retentionMetrics.GlobalAverageLifespanDays > 0
            ? Math.Round((decimal)((retentionMetrics.ReferredAverageLifespanDays - retentionMetrics.GlobalAverageLifespanDays) / retentionMetrics.GlobalAverageLifespanDays * 100.0), 1)
            : 0m;

        var retentionDto = new ReferralRetentionKpisDto(
            ReferredAverageLifespanDays: retentionMetrics.ReferredAverageLifespanDays,
            GlobalAverageLifespanDays: retentionMetrics.GlobalAverageLifespanDays,
            ReferredRetentionRatePercentage: retentionMetrics.ReferredRetentionRatePercentage,
            GlobalRetentionRatePercentage: retentionMetrics.GlobalRetentionRatePercentage,
            LifespanAdvantagePercentage: lifespanAdvantage
        );

        stopwatch.Stop();
        ReferralTelemetry.OperationDurationHistogram.Record(stopwatch.ElapsedMilliseconds,
            new KeyValuePair<string, object?>("operation", "GetReferralProgramKpis"));

        var resultDto = new ReferralProgramKpisDto(
            PeriodStartDateUtc: request.StartDateUtc,
            PeriodEndDateUtc: request.EndDateUtc,
            CalculatedAtUtc: DateTime.UtcNow,
            Funnel: funnelDto,
            Cac: cacDto,
            Virality: viralityDto,
            Retention: retentionDto
        );

        return Result.Success(resultDto);
    }
}
