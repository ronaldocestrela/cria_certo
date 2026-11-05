using CriaCerto.Modules.Breeding.Application.Abstractions;
using CriaCerto.Modules.Breeding.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Api.Services;

public sealed class CompositeActiveHerdCounter : ITenantActiveHerdCounter
{
    private readonly IBreedingDbContext _breedingDbContext;
    private readonly ITenancyDbContext _tenancyDbContext;

    public CompositeActiveHerdCounter(IBreedingDbContext breedingDbContext, ITenancyDbContext tenancyDbContext)
    {
        _breedingDbContext = breedingDbContext;
        _tenancyDbContext = tenancyDbContext;
    }

    public async Task<int> GetActiveHeadCountAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var breedingCount = await _breedingDbContext.Cows
            .CountAsync(c => c.TenantId == tenantId &&
                             c.Status != ReproductiveStatus.Sold &&
                             c.Status != ReproductiveStatus.Culled, cancellationToken);

        var tenancyUnitCount = await _tenancyDbContext.ProductionUnits
            .Where(pu => pu.TenantId == tenantId)
            .SumAsync(pu => pu.CurrentHeadCount, cancellationToken);

        return Math.Max(breedingCount, tenancyUnitCount);
    }
}

