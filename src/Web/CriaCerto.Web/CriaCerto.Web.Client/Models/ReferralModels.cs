namespace CriaCerto.Web.Client.Models;

public sealed record CustomerReferralDashboardModel(
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
    IReadOnlyList<ReferredFarmSummaryModel> ReferredFarms);

public sealed record ReferredFarmSummaryModel(
    Guid AttributionId,
    string MaskedFarmName,
    DateTime AttributionDateUtc,
    DateTime? ConvertedAtUtc,
    string Status,
    string StatusDescription,
    string EstimatedBenefitDescription);

public sealed record CustomizeReferralCodeRequestModel(string NewCode);

public sealed record CustomizeReferralCodeResponseModel(string Code, string ReferralUrl);
