using CriaCerto.Modules.Referral.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Api.Services;

public sealed class TenantInfoLookupService : ITenantInfoLookupService
{
    private readonly TenancyDbContext _tenancyDbContext;

    public TenantInfoLookupService(TenancyDbContext tenancyDbContext)
    {
        _tenancyDbContext = tenancyDbContext;
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

        return new TenantSummaryDto(
            TenantId: tenant.Id,
            Name: tenant.Name,
            TradeName: tenant.LegalName,
            Status: tenant.Status,
            PlanCode: tenant.SubscribedPlan,
            PlanName: tenant.SubscribedPlan);
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

        return tenants.ToDictionary(
            t => t.Id,
            t => new TenantSummaryDto(
                TenantId: t.Id,
                Name: t.Name,
                TradeName: t.LegalName,
                Status: t.Status,
                PlanCode: t.SubscribedPlan,
                PlanName: t.SubscribedPlan));
    }
}
