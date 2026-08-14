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
        var query = _dbContext.Tenants.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            var cnpjDigits = CnpjNormalizer.Normalize(request.SearchTerm);
            query = query.Where(t =>
                t.Name.ToLower().Contains(term)
                || (t.LegalName != null && t.LegalName.ToLower().Contains(term))
                || t.CNPJ.ToLower().Contains(term)
                || t.CnpjNormalized.Contains(cnpjDigits)
                || (t.ExternalIdentifier != null && t.ExternalIdentifier.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim();
            query = query.Where(t => t.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.SubscribedPlan))
        {
            var plan = request.SubscribedPlan.Trim();
            query = query.Where(t => t.SubscribedPlan == plan);
        }

        if (!string.IsNullOrWhiteSpace(request.State))
        {
            var state = request.State.Trim().ToUpperInvariant();
            query = query.Where(t => t.State == state);
        }

        if (!string.IsNullOrWhiteSpace(request.OwnerSearch))
        {
            var ownerTerm = request.OwnerSearch.Trim().ToLower();
            query = query.Where(t =>
                (t.TechnicalOwnerName != null && t.TechnicalOwnerName.ToLower().Contains(ownerTerm))
                || (t.TechnicalOwnerEmail != null && t.TechnicalOwnerEmail.ToLower().Contains(ownerTerm))
                || (t.CommercialOwnerName != null && t.CommercialOwnerName.ToLower().Contains(ownerTerm))
                || (t.CommercialOwnerEmail != null && t.CommercialOwnerEmail.ToLower().Contains(ownerTerm)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var tenants = await query
            .OrderByDescending(t => t.CreatedAtUtc)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var items = tenants.Select(TenantBackofficeMapper.ToSummaryDto).ToList();

        return Result.Success(new PagedTenantBackofficeResult<TenantBackofficeSummaryDto>(
            items, totalCount, request.Page, request.PageSize));
    }
}
