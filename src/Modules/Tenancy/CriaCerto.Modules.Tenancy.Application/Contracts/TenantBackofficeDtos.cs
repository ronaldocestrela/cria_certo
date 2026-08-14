namespace CriaCerto.Modules.Tenancy.Application.Contracts;

public sealed record TenantBackofficeSummaryDto(
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

public sealed record TenantBackofficeDetailDto(
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

public sealed record PagedTenantBackofficeResult<T>(
    IReadOnlyCollection<T> Items,
    int TotalCount,
    int Page,
    int PageSize
);
