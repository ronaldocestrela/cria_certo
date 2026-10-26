namespace CriaCerto.Modules.Tenancy.Application.Features.ExportUserDataPortability.Dtos;

public sealed record UserDataExportDto(
    Guid Id,
    string FullName,
    string Email,
    string? PhoneNumber,
    bool IsEmailConfirmed,
    DateTime? EmailConfirmedAtUtc,
    DateTime CreatedAtUtc
);

public sealed record ConsentMetadataExportDto(
    string? TermsVersion,
    string? PrivacyPolicyVersion,
    DateTime? TermsAcceptedAtUtc,
    DateTime? PrivacyPolicyAcceptedAtUtc,
    string? ConsentIpAddress
);

public sealed record TenantAffiliationExportDto(
    Guid TenantId,
    string TenantName,
    string Role,
    DateTime JoinedAt,
    string SubscribedPlan
);

public sealed record UserAuditTrailEntryExportDto(
    DateTime TimestampUtc,
    string Action,
    string Category,
    string Severity,
    string IpAddress,
    string Details
);

public sealed record DataSubjectPortabilityDossierDto(
    string Specification,
    string Purpose,
    string ControllerName,
    DateTime ExportedAtUtc,
    UserDataExportDto UserProfile,
    ConsentMetadataExportDto ConsentHistory,
    IReadOnlyList<TenantAffiliationExportDto> AssociatedTenants,
    IReadOnlyList<UserAuditTrailEntryExportDto> AuditTrail
);

public sealed record PortabilityFileExportDto(
    string FileName,
    string ContentType,
    byte[] Content
);
