namespace CriaCerto.Modules.Tenancy.Application.Contracts;

public sealed record TenantOperationalTagDto(
    Guid Id,
    string Slug,
    string Name,
    string ColorHex,
    string Category
);

public sealed record OperationalTagDto(
    Guid Id,
    string Slug,
    string Name,
    string ColorHex,
    string Category,
    bool IsActive,
    DateTime CreatedAtUtc
);

public sealed record TenantSegmentationDto(
    string SizeSegment,
    string CommercialRegion,
    string ProductiveProfile,
    string ChurnRisk
);

public sealed record TenantExportRowDto(
    Guid Id,
    string Name,
    string CNPJ,
    string Status,
    string SubscribedPlan,
    string State,
    string SizeSegment,
    string CommercialRegion,
    string ProductiveProfile,
    string ChurnRisk,
    string Tags,
    string? TechnicalOwnerName,
    string? CommercialOwnerName,
    DateTime CreatedAtUtc
);

public sealed record TenantExportResultDto(
    byte[] Content,
    string FileName,
    int RowCount
);
