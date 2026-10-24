using CriaCerto.Modules.Referral.Application.Domain.Enums;

namespace CriaCerto.Modules.Referral.Application.Features.PartnerPortal.Dtos;

public record PartnerCampaignSummaryDto(
    Guid Id,
    string Code,
    string? CampaignTitle,
    long ClicksCount,
    bool IsActive,
    DateTime CreatedAtUtc
);

public record PartnerPortalDashboardDto(
    Guid ParticipantId,
    string FullName,
    string? TradeName,
    string Email,
    string PhoneNumber,
    PartnerCategory Category,
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
    CommissionType CommissionType,
    decimal CommissionValue,
    int? MaxRecurringMonths,
    int PaymentTermsDays,
    PixKeyType PixKeyType,
    string PixKey,
    IReadOnlyList<PartnerCampaignSummaryDto> Campaigns
);

public record PartnerStatementItemDto(
    Guid LedgerId,
    DateTime CreatedAtUtc,
    Guid ReferredTenantId,
    string ReferredTenantName,
    decimal InvoiceGrossAmount,
    string CommissionRateDescription,
    decimal NetCommissionAmount,
    string Currency,
    LedgerRewardStatus Status,
    DateTime? MaturityDateUtc,
    string? PayoutTransactionReference,
    string? PayoutReceiptFilePath,
    DateTime? PayoutRegisteredAtUtc
);

public record PartnerCommissionStatementDto(
    IReadOnlyList<PartnerStatementItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);

public record UpdatePartnerPixKeyRequest(
    PixKeyType PixKeyType,
    string PixKey
);

public record CreatePartnerCampaignCodeRequest(
    string CampaignTitle,
    string? PreferredSlug
);
