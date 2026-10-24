using CriaCerto.Modules.Referral.Application.Domain.Enums;

namespace CriaCerto.Modules.Referral.Application.Features.Payouts.Dtos;

public sealed record PayoutDashboardKpiDto(
    decimal TotalPendingMaturityAmount,
    int TotalPendingMaturityCount,
    decimal TotalAvailableForPayoutAmount,
    int TotalAvailablePartnersCount,
    decimal TotalPaidOutThisMonthAmount,
    int TotalPaidOutThisMonthCount
);

public sealed record PendingMaturityCommissionDto(
    Guid LedgerId,
    Guid ParticipantId,
    string PartnerName,
    string? TradeName,
    string LegalDocumentNumber,
    decimal NetAmount,
    string Currency,
    DateTime CreatedAtUtc,
    DateTime? MaturityDateUtc,
    int RemainingDays,
    string? StripeInvoiceId,
    string? ReferralCode
);

public sealed record AvailablePartnerPayoutDto(
    Guid ParticipantId,
    string PartnerName,
    string? TradeName,
    string LegalDocumentNumber,
    string PixKey,
    PixKeyType PixKeyType,
    decimal TotalAvailableAmount,
    int CommissionsCount,
    IReadOnlyList<CommissionDetailDto> Commissions
);

public sealed record CommissionDetailDto(
    Guid LedgerId,
    decimal NetAmount,
    string Currency,
    DateTime CreatedAtUtc,
    DateTime? MaturityDateUtc,
    string? StripeInvoiceId,
    Guid ReferralAttributionId
);

public sealed record PayoutHistoryDto(
    Guid LedgerId,
    Guid ParticipantId,
    string PartnerName,
    string LegalDocumentNumber,
    decimal NetAmount,
    string Currency,
    string? PayoutTransactionReference,
    string? PayoutReceiptFilePath,
    DateTime? PayoutRegisteredAtUtc,
    Guid? PayoutRegisteredByAdminId,
    string? StripeInvoiceId
);

public sealed record RegisterPartnerPayoutBatchRequest(
    Guid PartnerParticipantId,
    IReadOnlyList<Guid> LedgerIds,
    string TransactionReference
);

public sealed record CancelCommissionLedgerEntryRequest(
    string Reason
);
