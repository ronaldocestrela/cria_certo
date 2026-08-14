using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Contracts;
using CriaCerto.Modules.Tenancy.Application.Domain.Errors;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Tenancy.Application.Features.BackofficeTenants;

public record GetTenantBackofficeDetailQuery(Guid TenantId) : IRequest<Result<TenantBackofficeDetailDto>>;

public sealed class GetTenantBackofficeDetailQueryHandler
    : IRequestHandler<GetTenantBackofficeDetailQuery, Result<TenantBackofficeDetailDto>>
{
    private readonly ITenancyDbContext _dbContext;

    public GetTenantBackofficeDetailQueryHandler(ITenancyDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<TenantBackofficeDetailDto>> Handle(
        GetTenantBackofficeDetailQuery request,
        CancellationToken cancellationToken)
    {
        var tenant = await _dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.TenantId, cancellationToken);

        if (tenant is null)
        {
            return Result.Failure<TenantBackofficeDetailDto>(TenancyErrors.TenantNotFound);
        }

        var teamCount = await _dbContext.UserTenants.CountAsync(ut => ut.TenantId == tenant.Id, cancellationToken);
        var unitCount = await _dbContext.ProductionUnits.CountAsync(pu => pu.TenantId == tenant.Id, cancellationToken);

        var tags = await _dbContext.TenantOperationalTags
            .AsNoTracking()
            .Where(t => t.TenantId == tenant.Id && t.Tag.IsActive)
            .Select(t => new TenantOperationalTagDto(t.Tag.Id, t.Tag.Slug, t.Tag.Name, t.Tag.ColorHex, t.Tag.Category))
            .ToListAsync(cancellationToken);

        return Result.Success(TenantBackofficeMapper.ToDetailDto(tenant, teamCount, unitCount, tags));
    }
}
