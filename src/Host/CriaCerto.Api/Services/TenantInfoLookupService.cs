using CriaCerto.Modules.Backoffice.Infrastructure.Persistence;
using CriaCerto.Modules.Referral.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Api.Services;

public sealed class TenantInfoLookupService : ITenantInfoLookupService
{
    private readonly TenancyDbContext _tenancyDbContext;
    private readonly BackofficeDbContext? _backofficeDbContext;

    public TenantInfoLookupService(
        TenancyDbContext tenancyDbContext,
        BackofficeDbContext? backofficeDbContext = null)
    {
        _tenancyDbContext = tenancyDbContext;
        _backofficeDbContext = backofficeDbContext;
    }

    public async Task<TenantSummaryDto?> GetTenantSummaryAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await _tenancyDbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

        if (tenant == null)
        {
            return null;
        }

        decimal? planBasePrice = null;
        if (_backofficeDbContext != null && !string.IsNullOrWhiteSpace(tenant.SubscribedPlan))
        {
            var plan = await _backofficeDbContext.PlanCatalogs
                .AsNoTracking()
                .Include(p => p.Versions)
                .FirstOrDefaultAsync(p => p.Code.ToUpper() == tenant.SubscribedPlan.ToUpper(), cancellationToken);

            planBasePrice = plan?.Versions
                .OrderByDescending(v => v.VersionNumber)
                .Select(v => (decimal?)v.MonthlyPrice)
                .FirstOrDefault();
        }

        return new TenantSummaryDto(
            TenantId: tenant.Id,
            Name: tenant.Name,
            TradeName: tenant.LegalName,
            Status: tenant.Status,
            PlanCode: tenant.SubscribedPlan,
            PlanName: tenant.SubscribedPlan,
            CnpjNormalized: tenant.CnpjNormalized,
            TechnicalOwnerEmail: tenant.TechnicalOwnerEmail,
            CommercialOwnerEmail: tenant.CommercialOwnerEmail,
            StripeCustomerId: tenant.StripeCustomerId,
            PlanBasePrice: planBasePrice);
    }

    public async Task<IReadOnlyDictionary<Guid, TenantSummaryDto>> GetTenantSummariesAsync(
        IEnumerable<Guid> tenantIds,
        CancellationToken cancellationToken = default)
    {
        var idList = tenantIds.Distinct().ToList();
        if (idList.Count == 0)
        {
            return new Dictionary<Guid, TenantSummaryDto>();
        }

        var tenants = await _tenancyDbContext.Tenants
            .AsNoTracking()
            .Where(t => idList.Contains(t.Id))
            .ToListAsync(cancellationToken);

        var result = new Dictionary<Guid, TenantSummaryDto>();
        foreach (var t in tenants)
        {
            result[t.Id] = new TenantSummaryDto(
                TenantId: t.Id,
                Name: t.Name,
                TradeName: t.LegalName,
                Status: t.Status,
                PlanCode: t.SubscribedPlan,
                PlanName: t.SubscribedPlan,
                CnpjNormalized: t.CnpjNormalized,
                TechnicalOwnerEmail: t.TechnicalOwnerEmail,
                CommercialOwnerEmail: t.CommercialOwnerEmail,
                StripeCustomerId: t.StripeCustomerId);
        }

        return result;
    }

    public async Task<int> GetActiveTenantsCountAsync(CancellationToken cancellationToken = default)
    {
        return await _tenancyDbContext.Tenants
            .AsNoTracking()
            .CountAsync(t => t.Status == "Active", cancellationToken);
    }

    public async Task<TenantsRetentionMetricsDto> GetTenantsRetentionMetricsAsync(
        IEnumerable<Guid> referredTenantIds,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var allTenants = await _tenancyDbContext.Tenants
            .AsNoTracking()
            .Select(t => new
            {
                t.Id,
                t.Status,
                t.CreatedAtUtc,
                t.StatusChangedAtUtc
            })
            .ToListAsync(cancellationToken);

        if (allTenants.Count == 0)
        {
            return new TenantsRetentionMetricsDto(0, 0, 0m, 0m);
        }

        var referredSet = referredTenantIds.ToHashSet();
        var referredTenants = allTenants.Where(t => referredSet.Contains(t.Id)).ToList();

        // Cálculo Global
        double globalLifespanSum = allTenants.Sum(t =>
        {
            var end = (t.Status == "Active" ? now : (t.StatusChangedAtUtc ?? now));
            return Math.Max(0, (end - t.CreatedAtUtc).TotalDays);
        });
        double globalAvgLifespan = Math.Round(globalLifespanSum / allTenants.Count, 1);
        int globalActiveCount = allTenants.Count(t => t.Status == "Active");
        decimal globalRetentionRate = Math.Round((decimal)globalActiveCount / allTenants.Count * 100m, 1);

        // Cálculo dos Indicados
        double referredAvgLifespan = 0;
        decimal referredRetentionRate = 0m;

        if (referredTenants.Count > 0)
        {
            double referredLifespanSum = referredTenants.Sum(t =>
            {
                var end = (t.Status == "Active" ? now : (t.StatusChangedAtUtc ?? now));
                return Math.Max(0, (end - t.CreatedAtUtc).TotalDays);
            });
            referredAvgLifespan = Math.Round(referredLifespanSum / referredTenants.Count, 1);
            int referredActiveCount = referredTenants.Count(t => t.Status == "Active");
            referredRetentionRate = Math.Round((decimal)referredActiveCount / referredTenants.Count * 100m, 1);
        }

        return new TenantsRetentionMetricsDto(
            ReferredAverageLifespanDays: referredAvgLifespan,
            GlobalAverageLifespanDays: globalAvgLifespan,
            ReferredRetentionRatePercentage: referredRetentionRate,
            GlobalRetentionRatePercentage: globalRetentionRate);
    }
}
