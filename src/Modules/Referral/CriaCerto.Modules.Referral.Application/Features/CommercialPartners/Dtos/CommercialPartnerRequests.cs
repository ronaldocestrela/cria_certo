using CriaCerto.Modules.Referral.Application.Domain.Enums;

namespace CriaCerto.Modules.Referral.Application.Features.CommercialPartners.Dtos;

public record RegisterPartnerRequest(
    string FullName,
    string? TradeName,
    string Email,
    string PhoneNumber,
    PartnerCategory Category,
    string? City,
    string? State,
    string? Notes,
    string LegalDocumentNumber,
    PixKeyType PixKeyType,
    string PixKey,
    CommissionType CommissionType,
    decimal CommissionValue,
    int? MaxRecurringMonths,
    int? PaymentTermsDays,
    string? PreferredReferralCode
);

public record ProposeContractChangeRequest(
    CommissionType CommissionType,
    decimal CommissionValue,
    int? MaxRecurringMonths,
    int? PaymentTermsDays,
    string? PixKey,
    PixKeyType? PixKeyType,
    string Justification
);

public record UpdatePartnerProfileRequest(
    string FullName,
    string? TradeName,
    string Email,
    string PhoneNumber,
    PartnerCategory Category,
    string? City,
    string? State,
    string? Notes,
    string? PixKey = null,
    PixKeyType? PixKeyType = null
);

public record SuspendPartnerRequest(string Reason);
