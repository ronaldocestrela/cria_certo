using CriaCerto.Modules.Tenancy.Application.Contracts;
using CriaCerto.Modules.Tenancy.Application.Domain;

namespace CriaCerto.Modules.Tenancy.Application.Features.BackofficeTenants;

internal static class TenantBackofficeMapper
{
    public static TenantBackofficeSummaryDto ToSummaryDto(Tenant tenant) =>
        new(
            tenant.Id,
            tenant.Name,
            tenant.LegalName,
            tenant.CNPJ,
            tenant.ExternalIdentifier,
            tenant.Status,
            tenant.SubscribedPlan,
            tenant.Capacity,
            tenant.State,
            tenant.City,
            tenant.TechnicalOwnerName,
            tenant.CommercialOwnerName,
            tenant.CreatedAtUtc);

    public static TenantBackofficeDetailDto ToDetailDto(Tenant tenant, int teamMemberCount, int productionUnitCount)
    {
        var planLimit = PlanCapacityLimits.GetHeadCapacityLimit(tenant.SubscribedPlan);
        return new TenantBackofficeDetailDto(
            tenant.Id,
            tenant.Name,
            tenant.LegalName,
            tenant.CNPJ,
            tenant.ExternalIdentifier,
            tenant.Status,
            tenant.SubscribedPlan,
            tenant.Capacity,
            planLimit,
            tenant.Capacity > planLimit,
            tenant.State,
            tenant.City,
            tenant.StateRegistration,
            tenant.AreaInHectares,
            tenant.Type,
            tenant.TechnicalOwnerName,
            tenant.TechnicalOwnerEmail,
            tenant.CommercialOwnerName,
            tenant.CommercialOwnerEmail,
            teamMemberCount,
            productionUnitCount,
            tenant.CreatedAtUtc,
            tenant.UpdatedAtUtc);
    }
}
