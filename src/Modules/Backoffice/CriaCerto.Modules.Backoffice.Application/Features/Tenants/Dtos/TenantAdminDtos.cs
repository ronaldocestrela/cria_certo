namespace CriaCerto.Modules.Backoffice.Application.Features.Tenants.Dtos;

public sealed record TenantOperationalTagAdminDto(
    Guid Id,
    string Slug,
    string Name,
    string ColorHex,
    string Category
);

public sealed record OperationalTagAdminDto(
    Guid Id,
    string Slug,
    string Name,
    string ColorHex,
    string Category,
    bool IsActive,
    DateTime CreatedAtUtc
);

public sealed record TenantAdminSummaryDto(
    Guid Id,
    string Name,
    string? LegalName,
    string CNPJ,
    string? ExternalIdentifier,
    string Status,
    string SubscribedPlan,
    int Capacity,
    string State,
    string City,
    string SizeSegment,
    string CommercialRegion,
    string ProductiveProfile,
    string ChurnRisk,
    IReadOnlyCollection<TenantOperationalTagAdminDto> Tags,
    string? TechnicalOwnerName,
    string? CommercialOwnerName,
    bool IsProtected,
    DateTime CreatedAtUtc
);

public sealed record TenantAdminDetailDto(
    Guid Id,
    string Name,
    string? LegalName,
    string CNPJ,
    string? ExternalIdentifier,
    string Status,
    string SubscribedPlan,
    int Capacity,
    int PlanHeadCapacityLimit,
    bool IsOverPlanLimit,
    string State,
    string City,
    string StateRegistration,
    decimal AreaInHectares,
    string Type,
    string SizeSegment,
    string CommercialRegion,
    string ProductiveProfile,
    string ChurnRisk,
    IReadOnlyCollection<TenantOperationalTagAdminDto> Tags,
    string? TechnicalOwnerName,
    string? TechnicalOwnerEmail,
    string? CommercialOwnerName,
    string? CommercialOwnerEmail,
    bool IsProtected,
    string? StatusReason,
    DateTime? StatusChangedAtUtc,
    int TeamMemberCount,
    int ProductionUnitCount,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc
);

public sealed record PagedTenantAdminResult(
    IReadOnlyCollection<TenantAdminSummaryDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);

public sealed record TenantAdminFilterDto(
    string? SearchTerm = null,
    string? Status = null,
    string? SubscribedPlan = null,
    string? State = null,
    string? OwnerSearch = null,
    string? SizeSegment = null,
    string? CommercialRegion = null,
    string? ProductiveProfile = null,
    string? ChurnRisk = null,
    IReadOnlyCollection<Guid>? TagIds = null,
    bool IncludeInactiveTags = false
);

public sealed record AdminSavedFilterDto(
    Guid Id,
    string Name,
    TenantAdminFilterDto Filter,
    bool IsDefault,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc
);

public sealed record CreateTenantAdminRequest(
    string Name,
    string? LegalName,
    string CNPJ,
    string? ExternalIdentifier,
    string State,
    string City,
    string StateRegistration,
    decimal AreaInHectares,
    string SubscribedPlan,
    int Capacity,
    string Type,
    string? TechnicalOwnerName,
    string? TechnicalOwnerEmail,
    string? CommercialOwnerName,
    string? CommercialOwnerEmail,
    string? OwnerUserEmail,
    string? InitialStatus = null
);

public sealed record UpdateTenantAdminRequest(
    string Name,
    string? LegalName,
    string CNPJ,
    string? ExternalIdentifier,
    string State,
    string City,
    string StateRegistration,
    decimal AreaInHectares,
    int Capacity,
    string Type,
    string? TechnicalOwnerName,
    string? TechnicalOwnerEmail,
    string? CommercialOwnerName,
    string? CommercialOwnerEmail
);

public sealed record UpdateTenantSegmentationAdminRequest(
    string SizeSegment,
    string CommercialRegion,
    string ProductiveProfile,
    string ChurnRisk
);

public sealed record ReplaceTenantTagsAdminRequest(IReadOnlyCollection<Guid> TagIds);

public sealed record CreateOperationalTagAdminRequest(
    string Name,
    string Category,
    string? ColorHex
);

public sealed record SaveAdminFilterRequest(
    string Name,
    TenantAdminFilterDto Filter,
    bool IsDefault = false
);

public sealed record TenantLifecycleActionRequest(string Reason);

public sealed record TenantProtectionRequest(bool IsProtected, string Reason);
