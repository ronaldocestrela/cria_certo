using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;
using CriaCerto.Modules.Backoffice.Application.Features.Plans.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Backoffice.Application.Features.Plans.Queries;

public record GetHeadTierCatalogsQuery(bool IncludeInactive = false) : IRequest<Result<IReadOnlyList<HeadTierCatalogDto>>>;

public record GetModuleCatalogsQuery(bool IncludeInactive = false) : IRequest<Result<IReadOnlyList<ModuleCatalogDto>>>;

public record GetModuleCatalogByIdQuery(Guid ModuleCatalogId) : IRequest<Result<ModuleCatalogDto>>;

public sealed class GetModularCatalogQueriesHandler :
    IRequestHandler<GetHeadTierCatalogsQuery, Result<IReadOnlyList<HeadTierCatalogDto>>>,
    IRequestHandler<GetModuleCatalogsQuery, Result<IReadOnlyList<ModuleCatalogDto>>>,
    IRequestHandler<GetModuleCatalogByIdQuery, Result<ModuleCatalogDto>>
{
    private readonly DbContext _dbContext;

    public GetModularCatalogQueriesHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<IReadOnlyList<HeadTierCatalogDto>>> Handle(GetHeadTierCatalogsQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.Set<HeadTierCatalog>().AsNoTracking();

        if (!request.IncludeInactive)
        {
            query = query.Where(t => t.IsActive);
        }

        var tiers = await query
            .OrderBy(t => t.SortOrder)
            .ThenBy(t => t.MinHeads)
            .ToListAsync(cancellationToken);

        var dtos = tiers.Select(t => t.ToDto()).ToList();
        return Result.Success<IReadOnlyList<HeadTierCatalogDto>>(dtos);
    }

    public async Task<Result<IReadOnlyList<ModuleCatalogDto>>> Handle(GetModuleCatalogsQuery request, CancellationToken cancellationToken)
    {
        var tiersMap = await _dbContext.Set<HeadTierCatalog>()
            .AsNoTracking()
            .ToDictionaryAsync(t => t.Id, t => t.TierCode, cancellationToken);

        var query = _dbContext.Set<ModuleCatalog>()
            .Include(m => m.TierPrices)
            .AsNoTracking();

        if (!request.IncludeInactive)
        {
            query = query.Where(m => m.IsActive);
        }

        var modules = await query
            .OrderBy(m => m.SortOrder)
            .ThenBy(m => m.Name)
            .ToListAsync(cancellationToken);

        var dtos = modules.Select(m => m.ToDto(tiersMap)).ToList();
        return Result.Success<IReadOnlyList<ModuleCatalogDto>>(dtos);
    }

    public async Task<Result<ModuleCatalogDto>> Handle(GetModuleCatalogByIdQuery request, CancellationToken cancellationToken)
    {
        var module = await _dbContext.Set<ModuleCatalog>()
            .Include(m => m.TierPrices)
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == request.ModuleCatalogId, cancellationToken);

        if (module is null)
        {
            return Result.Failure<ModuleCatalogDto>(PlanErrors.ModuleCatalogNotFound);
        }

        var tiersMap = await _dbContext.Set<HeadTierCatalog>()
            .AsNoTracking()
            .ToDictionaryAsync(t => t.Id, t => t.TierCode, cancellationToken);

        return Result.Success(module.ToDto(tiersMap));
    }
}

