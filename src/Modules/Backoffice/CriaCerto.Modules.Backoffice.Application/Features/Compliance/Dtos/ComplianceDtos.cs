namespace CriaCerto.Modules.Backoffice.Application.Features.Compliance.Dtos;

public sealed record ComplianceOverviewDto(
    int PiiAccessLast24Hours,
    int PiiUnmasksLast30Days,
    int OperatorsWithUnmaskPermissionCount,
    int ProtectedTenantsCount,
    bool IsForensicTrailValid,
    Dictionary<string, int> UnmasksByRole,
    DateTime CheckedAtUtc
);

public sealed record AccessTrailItemDto(
    Guid Id,
    DateTime TimestampUtc,
    Guid AdminUserId,
    string AdminUserEmail,
    string? ActorRole,
    string Action,
    string Category,
    string Severity,
    string Resource,
    Guid? TargetTenantId,
    string? TargetTenantName,
    string IpAddress,
    string? Justification,
    string RecordHash,
    bool IsIntegrityValid
);

public sealed record PagedAccessTrailDto(
    IReadOnlyList<AccessTrailItemDto> Items,
    int TotalCount,
    int PageNumber,
    int PageSize,
    int TotalPages
);

public sealed record RevealSensitiveDataRequest(
    string EntityType,
    Guid EntityId,
    string FieldName,
    string Justification
);

public sealed record RevealedDataResultDto(
    string FieldName,
    string PlainValue,
    string MaskedValue,
    Guid AuditLogId,
    DateTime RevealedAtUtc
);

public sealed record ExportAccessTrailRequest(
    Guid? TargetTenantId = null,
    string? ActorEmail = null,
    DateTime? DateFromUtc = null,
    DateTime? DateToUtc = null,
    string Purpose = "Auditoria Externa LGPD",
    string Format = "CSV"
);

public sealed record ComplianceDossierExportDto(
    string FileName,
    string ContentType,
    byte[] Content,
    string Sha256Hash,
    DateTime GeneratedAtUtc
);
