using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Contracts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Tenancy.Application.Features.GetTenantProfile;

public record GetTenantProfileQuery(Guid TenantId) : IRequest<Result<TenantProfileDto>>;

public class GetTenantProfileQueryHandler : IRequestHandler<GetTenantProfileQuery, Result<TenantProfileDto>>
{
    private readonly ITenancyDbContext _dbContext;

    public GetTenantProfileQueryHandler(ITenancyDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<TenantProfileDto>> Handle(GetTenantProfileQuery request, CancellationToken cancellationToken)
    {
        var tenant = await _dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.TenantId, cancellationToken);

        if (tenant is null)
        {
            return Result.Failure<TenantProfileDto>(Error.NotFound("Tenant.NotFound", $"Organização/Fazenda com ID '{request.TenantId}' não foi encontrada."));
        }

        var dto = new TenantProfileDto(
            tenant.Id,
            tenant.Name,
            tenant.CNPJ,
            tenant.Status,
            tenant.SubscribedPlan,
            tenant.Capacity,
            tenant.State,
            tenant.City,
            tenant.StateRegistration,
            tenant.AreaInHectares,
            tenant.Type,
            tenant.StripeCustomerId,
            tenant.StripeSubscriptionId,
            tenant.CurrentPeriodEndUtc,
            tenant.CancelAtPeriodEnd
        );

        return Result.Success(dto);
    }
}
