using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;
using CriaCerto.Modules.Backoffice.Application.Features.Plans.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Backoffice.Application.Features.Plans.Queries;

public record GetPlanCatalogsQuery(bool IncludeArchived = false) : IRequest<Result<IReadOnlyList<PlanCatalogDto>>>;

public record GetPlanCatalogByIdQuery(Guid PlanCatalogId) : IRequest<Result<PlanCatalogDto>>;

public record ComparePlanVersionsQuery(Guid BaseVersionId, Guid TargetVersionId) : IRequest<Result<PlanVersionComparisonDto>>;

public sealed class GetPlanCatalogQueriesHandler :
    IRequestHandler<GetPlanCatalogsQuery, Result<IReadOnlyList<PlanCatalogDto>>>,
    IRequestHandler<GetPlanCatalogByIdQuery, Result<PlanCatalogDto>>,
    IRequestHandler<ComparePlanVersionsQuery, Result<PlanVersionComparisonDto>>
{
    private readonly DbContext _dbContext;

    public GetPlanCatalogQueriesHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<IReadOnlyList<PlanCatalogDto>>> Handle(GetPlanCatalogsQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.Set<PlanCatalog>()
            .Include(p => p.Versions)
                .ThenInclude(v => v.Features)
            .Include(p => p.Versions)
                .ThenInclude(v => v.Limits)
            .AsNoTracking();

        if (!request.IncludeArchived)
        {
            query = query.Where(p => !p.IsArchived);
        }

        var plans = await query.ToListAsync(cancellationToken);
        var dtos = plans.Select(p => p.ToDto()).ToList();
        return Result.Success<IReadOnlyList<PlanCatalogDto>>(dtos);
    }

    public async Task<Result<PlanCatalogDto>> Handle(GetPlanCatalogByIdQuery request, CancellationToken cancellationToken)
    {
        var plan = await _dbContext.Set<PlanCatalog>()
            .Include(p => p.Versions)
                .ThenInclude(v => v.Features)
            .Include(p => p.Versions)
                .ThenInclude(v => v.Limits)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.PlanCatalogId, cancellationToken);

        if (plan is null)
        {
            return Result.Failure<PlanCatalogDto>(PlanErrors.PlanNotFound);
        }

        return Result.Success(plan.ToDto());
    }

    public async Task<Result<PlanVersionComparisonDto>> Handle(ComparePlanVersionsQuery request, CancellationToken cancellationToken)
    {
        var baseVersion = await _dbContext.Set<PlanVersion>()
            .Include(v => v.Features)
            .Include(v => v.Limits)
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == request.BaseVersionId, cancellationToken);

        var targetVersion = await _dbContext.Set<PlanVersion>()
            .Include(v => v.Features)
            .Include(v => v.Limits)
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == request.TargetVersionId, cancellationToken);

        if (baseVersion is null || targetVersion is null)
        {
            return Result.Failure<PlanVersionComparisonDto>(PlanErrors.VersionNotFound);
        }

        var baseDto = baseVersion.ToDto();
        var targetDto = targetVersion.ToDto();

        var baseFeatures = baseVersion.Features.Where(f => f.IsEnabled).Select(f => f.FeatureKey).ToHashSet();
        var targetFeatures = targetVersion.Features.Where(f => f.IsEnabled).Select(f => f.FeatureKey).ToHashSet();

        var addedFeatures = targetFeatures.Except(baseFeatures).ToList();
        var removedFeatures = baseFeatures.Except(targetFeatures).ToList();

        var changedLimits = new List<string>();
        if (baseVersion.HeadCapacityLimit != targetVersion.HeadCapacityLimit)
        {
            changedLimits.Add($"Limite Cabeças: {baseVersion.HeadCapacityLimit} -> {targetVersion.HeadCapacityLimit}");
        }
        if (baseVersion.MaxUsers != targetVersion.MaxUsers)
        {
            changedLimits.Add($"Max Usuários: {baseVersion.MaxUsers?.ToString() ?? "Ilimitado"} -> {targetVersion.MaxUsers?.ToString() ?? "Ilimitado"}");
        }

        var priceDiffMonthly = targetVersion.MonthlyPrice - baseVersion.MonthlyPrice;
        var priceDiffAnnual = targetVersion.AnnualPriceMonthly - baseVersion.AnnualPriceMonthly;

        var comparison = new PlanVersionComparisonDto(
            baseDto,
            targetDto,
            addedFeatures,
            removedFeatures,
            changedLimits,
            priceDiffMonthly,
            priceDiffAnnual
        );

        return Result.Success(comparison);
    }
}
