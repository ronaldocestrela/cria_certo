using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Features.Tenants.Dtos;
using CriaCerto.Modules.Tenancy.Application.Features.BackofficeTenants;
using MediatR;

namespace CriaCerto.Modules.Backoffice.Application.Features.Tenants.Queries;

public record GetTenantsAdminQuery(
    string? SearchTerm = null,
    string? Status = null,
    string? SubscribedPlan = null,
    string? State = null,
    string? OwnerSearch = null,
    int Page = 1,
    int PageSize = 20
) : IRequest<Result<PagedTenantAdminResult>>;

public sealed class GetTenantsAdminQueryHandler : IRequestHandler<GetTenantsAdminQuery, Result<PagedTenantAdminResult>>
{
    private readonly ISender _sender;

    public GetTenantsAdminQueryHandler(ISender sender)
    {
        _sender = sender;
    }

    public async Task<Result<PagedTenantAdminResult>> Handle(GetTenantsAdminQuery request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetTenantsBackofficeQuery(
            request.SearchTerm,
            request.Status,
            request.SubscribedPlan,
            request.State,
            request.OwnerSearch,
            request.Page,
            request.PageSize), cancellationToken);

        if (result.IsFailure)
        {
            return Result.Failure<PagedTenantAdminResult>(result.Error);
        }

        return Result.Success(TenantAdminMapper.ToPagedResult(result.Value));
    }
}

public record GetTenantAdminDetailQuery(Guid TenantId) : IRequest<Result<TenantAdminDetailDto>>;

public sealed class GetTenantAdminDetailQueryHandler : IRequestHandler<GetTenantAdminDetailQuery, Result<TenantAdminDetailDto>>
{
    private readonly ISender _sender;

    public GetTenantAdminDetailQueryHandler(ISender sender)
    {
        _sender = sender;
    }

    public async Task<Result<TenantAdminDetailDto>> Handle(GetTenantAdminDetailQuery request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetTenantBackofficeDetailQuery(request.TenantId), cancellationToken);
        if (result.IsFailure)
        {
            return Result.Failure<TenantAdminDetailDto>(result.Error);
        }

        return Result.Success(TenantAdminMapper.ToDetailDto(result.Value));
    }
}
