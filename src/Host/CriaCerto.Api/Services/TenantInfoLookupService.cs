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
}
