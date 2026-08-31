using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Services;
using CriaCerto.Modules.Backoffice.Application.Features.Tenants.Dtos;
using CriaCerto.Modules.Tenancy.Application.Contracts;
using CriaCerto.Modules.Tenancy.Application.Features.BackofficeTenants;
using MediatR;

namespace CriaCerto.Modules.Backoffice.Application.Features.Tenants.Queries;

public record GetTenantsAdminQuery(
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
) : IRequest<Result<PagedTenantAdminResult>>;

public sealed class GetTenantsAdminQueryHandler : IRequestHandler<GetTenantsAdminQuery, Result<PagedTenantAdminResult>>
{
    private readonly ISender _sender;
    private readonly IPiiDataMasker? _masker;

    public GetTenantsAdminQueryHandler(ISender sender, IPiiDataMasker? masker = null)
    {
        _sender = sender;
        _masker = masker;
    }

    public async Task<Result<PagedTenantAdminResult>> Handle(GetTenantsAdminQuery request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetTenantsBackofficeQuery(
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
            request.AfterCreatedAtUtc,
            request.AfterId,
            request.Page,
            request.PageSize), cancellationToken);

        if (result.IsFailure)
        {
            return Result.Failure<PagedTenantAdminResult>(result.Error);
        }

        return Result.Success(TenantAdminMapper.ToPagedResult(result.Value, _masker));
    }
}

public record GetTenantAdminDetailQuery(Guid TenantId) : IRequest<Result<TenantAdminDetailDto>>;

public sealed class GetTenantAdminDetailQueryHandler : IRequestHandler<GetTenantAdminDetailQuery, Result<TenantAdminDetailDto>>
{
    private readonly ISender _sender;
    private readonly IPiiDataMasker? _masker;

    public GetTenantAdminDetailQueryHandler(ISender sender, IPiiDataMasker? masker = null)
    {
        _sender = sender;
        _masker = masker;
    }

    public async Task<Result<TenantAdminDetailDto>> Handle(GetTenantAdminDetailQuery request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetTenantBackofficeDetailQuery(request.TenantId), cancellationToken);
        if (result.IsFailure)
        {
            return Result.Failure<TenantAdminDetailDto>(result.Error);
        }

        return Result.Success(TenantAdminMapper.ToDetailDto(result.Value, _masker));
    }
}

public record GetOperationalTagsAdminQuery(bool IncludeInactive = false)
    : IRequest<Result<IReadOnlyCollection<OperationalTagAdminDto>>>;

public sealed class GetOperationalTagsAdminQueryHandler
    : IRequestHandler<GetOperationalTagsAdminQuery, Result<IReadOnlyCollection<OperationalTagAdminDto>>>
{
    private readonly ISender _sender;

    public GetOperationalTagsAdminQueryHandler(ISender sender)
    {
        _sender = sender;
    }

    public async Task<Result<IReadOnlyCollection<OperationalTagAdminDto>>> Handle(
        GetOperationalTagsAdminQuery request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetOperationalTagsQuery(request.IncludeInactive), cancellationToken);
        if (result.IsFailure)
        {
            return Result.Failure<IReadOnlyCollection<OperationalTagAdminDto>>(result.Error);
        }

        return Result.Success<IReadOnlyCollection<OperationalTagAdminDto>>(
            result.Value.Select(TenantAdminMapper.ToTagDto).ToList());
    }
}

public record ExportTenantsAdminQuery(
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
    bool IncludeInactiveTags = false
) : IRequest<Result<TenantExportResultDto>>;

public sealed class ExportTenantsAdminQueryHandler : IRequestHandler<ExportTenantsAdminQuery, Result<TenantExportResultDto>>
{
    private readonly ISender _sender;

    public ExportTenantsAdminQueryHandler(ISender sender)
    {
        _sender = sender;
    }

    public async Task<Result<TenantExportResultDto>> Handle(ExportTenantsAdminQuery request, CancellationToken cancellationToken)
    {
        return await _sender.Send(new ExportTenantsBackofficeQuery(
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
            request.IncludeInactiveTags), cancellationToken);
    }
}
