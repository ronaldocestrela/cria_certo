using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Infrastructure.Persistence;
using CriaCerto.Modules.Tenancy.Application.Features.GetSubscriptionPlans;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Backoffice.Infrastructure.Services;

public sealed class BackofficeSubscriptionPlansProvider : ISubscriptionPlansProvider
{
    private readonly BackofficeDbContext _dbContext;

    public BackofficeSubscriptionPlansProvider(BackofficeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<SubscriptionPlanDto>?> GetActivePlansAsync(CancellationToken cancellationToken = default)
    {
        var catalogs = await _dbContext.PlanCatalogs
            .Where(p => !p.IsArchived)
            .Include(p => p.Versions)
                .ThenInclude(v => v.Features)
            .Include(p => p.Versions)
                .ThenInclude(v => v.Limits)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (catalogs.Count == 0)
        {
            return null;
        }

        var result = new List<SubscriptionPlanDto>();

        foreach (var catalog in catalogs)
        {
            // If there is an active draft (currently being edited or created in backoffice),
            // or the published active version, use it so edits reflect immediately.
            var version = catalog.Versions.FirstOrDefault(v => v.Status == PlanVersionStatus.Draft)
                ?? catalog.Versions.FirstOrDefault(v => v.Status == PlanVersionStatus.Published)
                ?? catalog.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();

            if (version == null)
            {
                continue;
            }

            var enabledFeatures = version.Features
                .Where(f => f.IsEnabled)
                .ToList();

            var includedModules = enabledFeatures
                .Select(f => f.FeatureKey.StartsWith("Modules.", StringComparison.OrdinalIgnoreCase)
                    ? f.FeatureKey["Modules.".Length..]
                    : f.FeatureKey)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var featureDtos = enabledFeatures
                .Select(f => new SubscriptionPlanFeatureDto(
                    Key: f.FeatureKey,
                    Name: !string.IsNullOrWhiteSpace(f.DisplayName) ? f.DisplayName : f.FeatureKey,
                    IsEnabled: f.IsEnabled))
                .ToList();

            bool isPopular = catalog.Code.Equals("pro", StringComparison.OrdinalIgnoreCase)
                || catalog.Name.Contains("Profissional", StringComparison.OrdinalIgnoreCase)
                || catalog.Name.Contains("Pro", StringComparison.OrdinalIgnoreCase);

            string planId = !string.IsNullOrWhiteSpace(catalog.Code)
                ? char.ToUpperInvariant(catalog.Code[0]) + catalog.Code[1..]
                : catalog.Name;

            result.Add(new SubscriptionPlanDto(
                PlanId: planId,
                Name: catalog.Name,
                Description: catalog.Description,
                MonthlyPrice: version.MonthlyPrice,
                AnnualPriceMonthly: version.AnnualPriceMonthly,
                HeadCapacityLimit: version.HeadCapacityLimit,
                IncludedModules: includedModules,
                IsPopular: isPopular,
                Features: featureDtos
            ));
        }

        return result.OrderBy(p => p.MonthlyPrice).ToList();
    }

    public async Task<ModularSubscriptionCatalogDto?> GetModularCatalogAsync(CancellationToken cancellationToken = default)
    {
        var tiers = await _dbContext.HeadTierCatalogs
            .Where(t => t.IsActive)
            .OrderBy(t => t.SortOrder)
            .ThenBy(t => t.MinHeads)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var modules = await _dbContext.ModuleCatalogs
            .Where(m => m.IsActive)
            .Include(m => m.TierPrices)
            .OrderBy(m => m.SortOrder)
            .ThenBy(m => m.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (tiers.Count == 0 || modules.Count == 0)
        {
            return null;
        }

        var tierDtos = tiers
            .Select(t => new HeadTierOptionDto(
                TierCode: t.TierCode,
                DisplayName: t.DisplayName,
                MinHeads: t.MinHeads,
                MaxHeads: t.MaxHeads,
                SortOrder: t.SortOrder))
            .ToList();

        var moduleDtos = new List<CommercialModuleOptionDto>();

        foreach (var module in modules)
        {
            var pricesByTier = new List<ModuleTierPriceOptionDto>();

            foreach (var tier in tiers)
            {
                var tierPrice = module.TierPrices
                    .Where(p => p.HeadTierCatalogId == tier.Id)
                    .OrderByDescending(p => p.Status == PlanVersionStatus.Published)
                    .ThenByDescending(p => p.Status == PlanVersionStatus.Draft)
                    .ThenByDescending(p => p.VersionNumber)
                    .FirstOrDefault();

                if (tierPrice != null)
                {
                    pricesByTier.Add(new ModuleTierPriceOptionDto(
                        TierCode: tier.TierCode,
                        MonthlyPrice: tierPrice.MonthlyPrice,
                        AnnualPriceMonthly: tierPrice.AnnualPriceMonthly,
                        StripePriceIdMonthly: tierPrice.StripePriceIdMonthly,
                        StripePriceIdAnnual: tierPrice.StripePriceIdAnnual));
                }
            }

            moduleDtos.Add(new CommercialModuleOptionDto(
                ModuleKey: module.ModuleKey,
                Name: module.Name,
                Description: module.Description,
                Icon: NormalizeIcon(module.Icon, module.ModuleKey),
                IsCore: module.IsCore,
                SortOrder: module.SortOrder,
                PricesByTier: pricesByTier));
        }

        return new ModularSubscriptionCatalogDto(tierDtos, moduleDtos);
    }

    private static string NormalizeIcon(string? icon, string moduleKey)
    {
        if (!string.IsNullOrWhiteSpace(icon) && !icon.StartsWith("bi-", StringComparison.OrdinalIgnoreCase) && !icon.StartsWith("bi ", StringComparison.OrdinalIgnoreCase))
        {
            return icon.Trim();
        }

        var normalized = icon?.Trim().ToLowerInvariant() ?? string.Empty;
        return normalized switch
        {
            "bi-grid-1x2-fill" or "bi-grid" or "bi-cow" => "pets",
            "bi-heart-pulse-fill" or "bi-heart-pulse" => "favorite",
            "bi-stars" or "bi-star-fill" => "child_care",
            "bi-graph-up-arrow" or "bi-graph-up" => "trending_up",
            "bi-shield-fill-plus" or "bi-shield-plus" => "vaccines",
            "bi-basket2-fill" or "bi-basket" => "restaurant",
            "bi-bar-chart-line-fill" or "bi-bar-chart" => "insights",
            _ => moduleKey.ToLowerInvariant() switch
            {
                "cows" => "pets",
                "breeding" => "favorite",
                "calving" => "child_care",
                "growth" => "trending_up",
                "sanitary" => "vaccines",
                "nutrition" => "restaurant",
                "analytics" => "insights",
                _ => "widgets"
            }
        };
    }
}
