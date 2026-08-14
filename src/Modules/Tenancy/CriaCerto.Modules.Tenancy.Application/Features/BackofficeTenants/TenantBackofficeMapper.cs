using CriaCerto.Modules.Tenancy.Application.Contracts;
using CriaCerto.Modules.Tenancy.Application.Domain;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Tenancy.Application.Features.BackofficeTenants;

internal static class TenantBackofficeMapper
{
    public static TenantOperationalTagDto ToTagDto(OperationalTag tag) =>
        new(tag.Id, tag.Slug, tag.Name, tag.ColorHex, tag.Category);

    public static TenantBackofficeSummaryDto ToSummaryDto(Tenant tenant, IReadOnlyCollection<TenantOperationalTagDto>? tags = null) =>
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
            tenant.SizeSegment,
            tenant.CommercialRegion,
            tenant.ProductiveProfile,
            tenant.ChurnRisk,
            tags ?? Array.Empty<TenantOperationalTagDto>(),
            tenant.TechnicalOwnerName,
            tenant.CommercialOwnerName,
            tenant.IsProtected,
            tenant.CreatedAtUtc);

    public static TenantBackofficeDetailDto ToDetailDto(
        Tenant tenant,
        int teamMemberCount,
        int productionUnitCount,
        IReadOnlyCollection<TenantOperationalTagDto>? tags = null)
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
            tenant.SizeSegment,
            tenant.CommercialRegion,
            tenant.ProductiveProfile,
            tenant.ChurnRisk,
            tags ?? Array.Empty<TenantOperationalTagDto>(),
            tenant.TechnicalOwnerName,
            tenant.TechnicalOwnerEmail,
            tenant.CommercialOwnerName,
            tenant.CommercialOwnerEmail,
            tenant.IsProtected,
            tenant.StatusReason,
            tenant.StatusChangedAtUtc,
            teamMemberCount,
            productionUnitCount,
            tenant.CreatedAtUtc,
            tenant.UpdatedAtUtc);
    }

    public static async Task<IReadOnlyDictionary<Guid, List<TenantOperationalTagDto>>> LoadTagsByTenantIdsAsync(
        IQueryable<TenantOperationalTag> query,
        IReadOnlyCollection<Guid> tenantIds,
        CancellationToken cancellationToken)
    {
        if (tenantIds.Count == 0)
        {
            return new Dictionary<Guid, List<TenantOperationalTagDto>>();
        }

        var rows = await query
            .AsNoTracking()
            .Where(t => tenantIds.Contains(t.TenantId) && t.Tag.IsActive)
            .Select(t => new
            {
                t.TenantId,
                Tag = new TenantOperationalTagDto(t.Tag.Id, t.Tag.Slug, t.Tag.Name, t.Tag.ColorHex, t.Tag.Category)
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => r.TenantId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Tag).ToList());
    }
}
