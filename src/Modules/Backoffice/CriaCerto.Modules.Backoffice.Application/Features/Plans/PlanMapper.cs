using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Features.Plans.Dtos;

namespace CriaCerto.Modules.Backoffice.Application.Features.Plans;

public static class PlanMapper
{
    public static PlanFeatureDto ToDto(this PlanFeature feature) =>
        new(feature.Id, feature.FeatureKey, feature.DisplayName, feature.IsEnabled, feature.FeatureType);

    public static PlanLimitDto ToDto(this PlanLimit limit) =>
        new(limit.Id, limit.LimitKey, limit.LimitValue, limit.Unit);

    public static PlanVersionDto ToDto(this PlanVersion version)
    {
        return new PlanVersionDto(
            version.Id,
            version.PlanCatalogId,
            version.VersionNumber,
            version.VersionName,
            version.Status.ToString(),
            version.MonthlyPrice,
            version.AnnualPriceMonthly,
            version.HeadCapacityLimit,
            version.MaxUsers,
            version.MaxProductionUnits,
            version.EffectiveFrom,
            version.EffectiveTo,
            version.PublishedAtUtc,
            version.PublishedByAdminId,
            version.ApprovalNotes,
            version.CreatedAtUtc,
            version.Features.Select(f => f.ToDto()).ToList(),
            version.Limits.Select(l => l.ToDto()).ToList()
        );
    }

    public static PlanCatalogDto ToDto(this PlanCatalog plan)
    {
        var versions = plan.Versions.Select(v => v.ToDto()).ToList();
        var activeVersion = versions.FirstOrDefault(v => v.Status == PlanVersionStatus.Published.ToString());
        var draftVersion = versions.FirstOrDefault(v => v.Status == PlanVersionStatus.Draft.ToString());

        return new PlanCatalogDto(
            plan.Id,
            plan.Code,
            plan.Name,
            plan.Description,
            plan.Category,
            plan.IsArchived,
            plan.CreatedAtUtc,
            plan.UpdatedAtUtc,
            activeVersion,
            draftVersion,
            versions
        );
    }

    public static HeadTierCatalogDto ToDto(this HeadTierCatalog tier) =>
        new(
            tier.Id,
            tier.TierCode,
            tier.DisplayName,
            tier.MinHeads,
            tier.MaxHeads,
            tier.SortOrder,
            tier.IsActive,
            tier.CreatedAtUtc,
            tier.UpdatedAtUtc);

    public static ModuleTierPriceDto ToDto(this ModuleTierPrice price, string tierCode = "") =>
        new(
            price.Id,
            price.ModuleCatalogId,
            price.HeadTierCatalogId,
            tierCode,
            price.VersionNumber,
            price.Status.ToString(),
            price.MonthlyPrice,
            price.AnnualPriceMonthly,
            price.StripePriceIdMonthly,
            price.StripePriceIdAnnual,
            price.PublishedAtUtc,
            price.PublishedByAdminId,
            price.ApprovalNotes,
            price.CreatedAtUtc);

    public static ModuleCatalogDto ToDto(
        this ModuleCatalog module,
        IReadOnlyDictionary<Guid, string>? tierCodesById = null)
    {
        var allPrices = module.TierPrices
            .Select(p =>
            {
                var tierCode = tierCodesById is not null && tierCodesById.TryGetValue(p.HeadTierCatalogId, out var code)
                    ? code
                    : string.Empty;
                return p.ToDto(tierCode);
            })
            .OrderBy(p => p.TierCode)
            .ThenByDescending(p => p.VersionNumber)
            .ToList();

        var activePrices = allPrices
            .Where(p => p.Status == PlanVersionStatus.Published.ToString())
            .ToList();

        var draftPrices = allPrices
            .Where(p => p.Status == PlanVersionStatus.Draft.ToString())
            .ToList();

        return new ModuleCatalogDto(
            module.Id,
            module.ModuleKey,
            module.Name,
            module.Description,
            module.Icon,
            module.IsCore,
            module.IsActive,
            module.SortOrder,
            module.CreatedAtUtc,
            module.UpdatedAtUtc,
            activePrices,
            draftPrices,
            allPrices);
    }
}
