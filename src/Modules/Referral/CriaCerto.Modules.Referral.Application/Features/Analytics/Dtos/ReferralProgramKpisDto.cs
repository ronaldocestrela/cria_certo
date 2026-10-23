namespace CriaCerto.Modules.Referral.Application.Features.Analytics.Dtos;

public record ReferralFunnelKpisDto(
    long TotalClicks,
    long CustomerClicks,
    long PartnerClicks,
    int TotalAttributions,
    int ConvertedAttributions,
    int PendingAttributions,
    int FraudulentAttributions,
    int ChurnedAttributions,
    decimal ClickToLeadConversionRate,
    decimal LeadToPaidConversionRate,
    decimal GlobalConversionRate
);

public record ReferralCacKpisDto(
    decimal CustomerCac,
    decimal PartnerCac,
    decimal PaidMediaCac,
    decimal BlendedCac,
    decimal CustomerTotalRewardSpent,
    decimal PartnerTotalCommissionSpent,
    decimal PaidMediaTotalSpent,
    int CustomerAcquisitionsCount,
    int PartnerAcquisitionsCount,
    int PaidMediaAcquisitionsCount
);

public record ReferralViralityKpisDto(
    double KFactor,
    string ViralStatus,
    int ActivePlatformTenantsCount,
    int NewCustomersFromCustomerReferrals
);

public record ReferralRetentionKpisDto(
    double ReferredAverageLifespanDays,
    double GlobalAverageLifespanDays,
    decimal ReferredRetentionRatePercentage,
    decimal GlobalRetentionRatePercentage,
    decimal LifespanAdvantagePercentage
);

public record ReferralProgramKpisDto(
    DateTime? PeriodStartDateUtc,
    DateTime? PeriodEndDateUtc,
    DateTime CalculatedAtUtc,
    ReferralFunnelKpisDto Funnel,
    ReferralCacKpisDto Cac,
    ReferralViralityKpisDto Virality,
    ReferralRetentionKpisDto Retention
);
