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
}
