using CriaCerto.Modules.Backoffice.Infrastructure.Persistence;
using CriaCerto.Modules.Referral.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Api.Services;

public sealed class PlanCatalogLookupService : IPlanCatalogLookupService
{
    private readonly BackofficeDbContext _backofficeDbContext;

    public PlanCatalogLookupService(BackofficeDbContext backofficeDbContext)
    {
        _backofficeDbContext = backofficeDbContext;
    }

    public async Task<IReadOnlyList<PlanLookupDto>> GetAvailablePlansAsync(CancellationToken cancellationToken = default)
    {
        var plans = await _backofficeDbContext.PlanCatalogs
            .AsNoTracking()
            .Where(p => !p.IsArchived)
            .Select(p => new PlanLookupDto(p.Id, p.Code, p.Name, p.IsArchived))
            .ToListAsync(cancellationToken);

        return plans;
    }
}
