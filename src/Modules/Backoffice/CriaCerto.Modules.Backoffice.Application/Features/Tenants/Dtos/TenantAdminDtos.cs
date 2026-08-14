namespace CriaCerto.Modules.Backoffice.Application.Features.Tenants.Dtos;

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
    string? TechnicalOwnerName,
    string? CommercialOwnerName,
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
    string? TechnicalOwnerName,
    string? TechnicalOwnerEmail,
    string? CommercialOwnerName,
    string? CommercialOwnerEmail,
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
    string? OwnerUserEmail
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
