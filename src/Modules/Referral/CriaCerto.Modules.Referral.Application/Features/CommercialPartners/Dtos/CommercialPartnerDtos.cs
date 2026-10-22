using CriaCerto.Modules.Referral.Application.Domain.Enums;

namespace CriaCerto.Modules.Referral.Application.Features.CommercialPartners.Dtos;

public record CommercialPartnerDto(
    Guid Id,
    Guid ParticipantId,
    Guid? UserId,
    string FullName,
    string? TradeName,
    string Email,
    string PhoneNumber,
    PartnerCategory Category,
    string? City,
    string? State,
    string? Notes,
    ParticipantStatus Status,
    string? SuspensionReason,
    string LegalDocumentNumber,
    PixKeyType PixKeyType,
    string PixKey,
    CommissionType CommissionType,
    decimal CommissionValue,
    int? MaxRecurringMonths,
    int PaymentTermsDays,
    string ReferralCode,
    long ClicksCount,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    IReadOnlyList<PartnerContractHistoryDto> ContractHistory
);

public record CommercialPartnerListItemDto(
    Guid Id,
    Guid ParticipantId,
    string FullName,
    string? TradeName,
    string Email,
    string PhoneNumber,
    PartnerCategory Category,
    string LegalDocumentNumber,
    ParticipantStatus Status,
    CommissionType CommissionType,
    decimal CommissionValue,
    int? MaxRecurringMonths,
    string PixKey,
    PixKeyType PixKeyType,
    string ReferralCode,
    long ClicksCount,
    int ActiveReferralsCount,
    DateTime CreatedAtUtc
);

public record PartnerContractHistoryDto(
    Guid Id,
    CommissionType CommissionType,
    decimal CommissionValue,
    int? MaxRecurringMonths,
    int PaymentTermsDays,
    string PixKey,
    PixKeyType PixKeyType,
    string LegalDocumentNumber,
    DateTime ValidFromUtc,
    DateTime? ValidToUtc,
    bool IsActive,
    DateTime CreatedAtUtc
);

public record SuggestReferralCodeDto(
    string SuggestedCode,
    bool IsAvailable
);
