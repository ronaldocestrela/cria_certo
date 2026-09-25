namespace CriaCerto.Modules.Tenancy.Application.Contracts;

public sealed record TenantProfileDto(
    Guid Id,
    string Name,
    string CNPJ,
    string Status,
    string SubscribedPlan,
    int Capacity,
    string State,
    string City,
    string StateRegistration,
    decimal AreaInHectares,
    string Type,
    string? StripeCustomerId = null,
    string? StripeSubscriptionId = null,
    DateTime? CurrentPeriodEndUtc = null,
    bool CancelAtPeriodEnd = false
);

public sealed record ProductionUnitDto(
    Guid Id,
    Guid TenantId,
    string Code,
    string Name,
    string Type,
    string Status,
    int Capacity,
    int CurrentHeadCount,
    string? LocationDetails,
    decimal OccupancyPercentage
);
