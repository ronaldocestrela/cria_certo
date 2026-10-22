namespace CriaCerto.Web.Client.Models;

public sealed record PartnerCampaignSummaryModel(
    Guid Id,
    string Code,
    string? CampaignTitle,
    long ClicksCount,
    bool IsActive,
    DateTime CreatedAtUtc
);

public sealed record PartnerPortalDashboardModel(
    Guid ParticipantId,
    string FullName,
    string? TradeName,
    string Email,
    string PhoneNumber,
    int Category,
    string LegalDocumentNumber,
    string MainReferralCode,
    long TotalClicks,
    int TrialSignupsCount,
    int PaidSubscriptionsCount,
    decimal ConversionRatePercentage,
    decimal PendingMaturityAmount,
    decimal AvailablePayoutAmount,
    decimal TotalPaidAmount,
    string Currency,
    int CommissionType,
    decimal CommissionValue,
    int? MaxRecurringMonths,
    int PaymentTermsDays,
    int PixKeyType,
    string PixKey,
    IReadOnlyList<PartnerCampaignSummaryModel> Campaigns
);

public sealed record PartnerStatementItemModel(
    Guid LedgerId,
    DateTime CreatedAtUtc,
    Guid ReferredTenantId,
    string ReferredTenantName,
    decimal InvoiceGrossAmount,
    string CommissionRateDescription,
    decimal NetCommissionAmount,
    string Currency,
    int Status,
    DateTime? MaturityDateUtc,
    string? PayoutTransactionReference,
    string? PayoutReceiptFilePath,
    DateTime? PayoutRegisteredAtUtc
);

public sealed record PartnerCommissionStatementModel(
    IReadOnlyList<PartnerStatementItemModel> Items,
    int TotalCount,
    int Page,
    int PageSize
);

public sealed record UpdatePartnerPixKeyRequestModel(
    int PixKeyType,
    string PixKey
);

public sealed record CreatePartnerCampaignCodeRequestModel(
    string CampaignTitle,
    string? PreferredSlug
);
