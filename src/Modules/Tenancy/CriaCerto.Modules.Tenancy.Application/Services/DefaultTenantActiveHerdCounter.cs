using CriaCerto.Modules.Tenancy.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Tenancy.Application.Services;

public sealed class DefaultTenantActiveHerdCounter : ITenantActiveHerdCounter
{
    private readonly ITenancyDbContext _dbContext;

    public DefaultTenantActiveHerdCounter(ITenancyDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<int> GetActiveHeadCountAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ProductionUnits
            .Where(pu => pu.TenantId == tenantId)
            .SumAsync(pu => pu.CurrentHeadCount, cancellationToken);
    }
}

