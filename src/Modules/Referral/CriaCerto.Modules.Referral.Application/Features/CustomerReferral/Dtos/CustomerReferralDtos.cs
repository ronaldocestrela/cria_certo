namespace CriaCerto.Modules.Referral.Application.Features.CustomerReferral.Dtos;

public sealed record CustomerReferralDashboardDto(
    string ReferralCode,
    string ReferralUrl,
    decimal AccumulatedDiscountNextInvoice,
    string FormattedAccumulatedDiscount,
    int ActiveReferralsCount,
    int PendingTrialReferralsCount,
    string? CurrentPlanCode,
    string? CurrentPlanName,
    string DiscountPerReferralDescription,
    string WhatsAppShareText,
    IReadOnlyList<ReferredFarmSummaryDto> ReferredFarms);

public sealed record ReferredFarmSummaryDto(
    Guid AttributionId,
    string MaskedFarmName,
    DateTime AttributionDateUtc,
    DateTime? ConvertedAtUtc,
    string Status,
    string StatusDescription,
    string EstimatedBenefitDescription);

public sealed record CustomizeReferralCodeRequest(string NewCode);

public sealed record CustomizeReferralCodeResponse(string Code, string ReferralUrl);
