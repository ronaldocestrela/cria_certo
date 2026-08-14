using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Contracts;
using CriaCerto.Modules.Tenancy.Application.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Tenancy.Application.Features.BackofficeTenants;

public record GetTenantsBackofficeQuery(
    string? SearchTerm = null,
    string? Status = null,
    string? SubscribedPlan = null,
    string? State = null,
    string? OwnerSearch = null,
    string? SizeSegment = null,
    string? CommercialRegion = null,
    string? ProductiveProfile = null,
    string? ChurnRisk = null,
    IReadOnlyCollection<Guid>? TagIds = null,
    bool IncludeInactiveTags = false,
    DateTime? AfterCreatedAtUtc = null,
    Guid? AfterId = null,
    int Page = 1,
    int PageSize = 20
) : IRequest<Result<PagedTenantBackofficeResult<TenantBackofficeSummaryDto>>>;

public sealed class GetTenantsBackofficeQueryHandler
    : IRequestHandler<GetTenantsBackofficeQuery, Result<PagedTenantBackofficeResult<TenantBackofficeSummaryDto>>>
{
    private readonly ITenancyDbContext _dbContext;

    public GetTenantsBackofficeQueryHandler(ITenancyDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<PagedTenantBackofficeResult<TenantBackofficeSummaryDto>>> Handle(
        GetTenantsBackofficeQuery request,
        CancellationToken cancellationToken)
    {
        var pageSize = TenantSegmentationCatalog.ClampPageSize(request.PageSize);
        var page = request.Page <= 0 ? 1 : request.Page;

        var query = TenantBackofficeQueryBuilder.ApplyFilters(
            _dbContext.Tenants.AsNoTracking(),
            request.SearchTerm,
            request.Status,
            request.SubscribedPlan,
            request.State,
            request.OwnerSearch,
            request.SizeSegment,
            request.CommercialRegion,
            request.ProductiveProfile,
            request.ChurnRisk,
            request.TagIds,
            request.IncludeInactiveTags,
            _dbContext.TenantOperationalTags);

        var totalCount = await query.CountAsync(cancellationToken);

        query = query
            .OrderByDescending(t => t.CreatedAtUtc)
            .ThenByDescending(t => t.Id);

        if (request.AfterCreatedAtUtc.HasValue && request.AfterId.HasValue)
        {
            var cursorCreatedAt = request.AfterCreatedAtUtc.Value;
            var cursorId = request.AfterId.Value;
            query = query.Where(t =>
                t.CreatedAtUtc < cursorCreatedAt
                || (t.CreatedAtUtc == cursorCreatedAt && t.Id.CompareTo(cursorId) < 0));
        }
        else if (page > 1)
        {
            query = query.Skip((page - 1) * pageSize);
        }

        var tenants = await query
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var tenantIds = tenants.Select(t => t.Id).ToList();
        var tagsByTenant = await TenantBackofficeMapper.LoadTagsByTenantIdsAsync(
            _dbContext.TenantOperationalTags,
            tenantIds,
            cancellationToken);

        var items = tenants
            .Select(t =>
            {
                tagsByTenant.TryGetValue(t.Id, out var tags);
                return TenantBackofficeMapper.ToSummaryDto(t, tags);
            })
            .ToList();

        return Result.Success(new PagedTenantBackofficeResult<TenantBackofficeSummaryDto>(
            items, totalCount, page, pageSize));
    }
}
