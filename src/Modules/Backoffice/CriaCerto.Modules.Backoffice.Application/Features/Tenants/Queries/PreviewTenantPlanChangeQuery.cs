using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;
using CriaCerto.Modules.Backoffice.Application.Features.Tenants.Dtos;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Domain.Errors;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CriaCerto.Modules.Backoffice.Application.Features.Tenants.Queries;

public sealed record PreviewTenantPlanChangeQuery(
    Guid TenantId,
    Guid TargetPlanVersionId
) : IRequest<Result<TenantPlanPreviewDto>>;

public sealed class PreviewTenantPlanChangeQueryHandler : IRequestHandler<PreviewTenantPlanChangeQuery, Result<TenantPlanPreviewDto>>
{
    private readonly ITenancyDbContext? _tenancyDbContext;
    private readonly DbContext? _backofficeDbContext;
    private readonly Func<Guid, Task<Tenant?>>? _tenantLookup;
    private readonly Func<Guid, Task<PlanVersion?>>? _planVersionLookup;

    [ActivatorUtilitiesConstructor]
    public PreviewTenantPlanChangeQueryHandler(
        ITenancyDbContext tenancyDbContext,
        DbContext backofficeDbContext)
    {
        _tenancyDbContext = tenancyDbContext;
        _backofficeDbContext = backofficeDbContext;
    }

    public PreviewTenantPlanChangeQueryHandler(
        Func<Guid, Task<Tenant?>> tenantLookup,
        Func<Guid, Task<PlanVersion?>> planVersionLookup)
    {
        _tenantLookup = tenantLookup;
        _planVersionLookup = planVersionLookup;
    }

    public async Task<Result<TenantPlanPreviewDto>> Handle(PreviewTenantPlanChangeQuery request, CancellationToken cancellationToken)
    {
        Tenant? tenant = _tenantLookup != null
            ? await _tenantLookup(request.TenantId)
            : await _tenancyDbContext!.Tenants.FirstOrDefaultAsync(t => t.Id == request.TenantId, cancellationToken);

        if (tenant is null)
        {
            return Result.Failure<TenantPlanPreviewDto>(TenancyErrors.TenantNotFound);
        }

        PlanVersion? targetVersion = _planVersionLookup != null
            ? await _planVersionLookup(request.TargetPlanVersionId)
            : await _backofficeDbContext!.Set<PlanVersion>()
                .Include(v => v.Features)
                .FirstOrDefaultAsync(v => v.Id == request.TargetPlanVersionId, cancellationToken);

        if (targetVersion is null)
        {
            return Result.Failure<TenantPlanPreviewDto>(TenancyErrors.PlanVersionNotFound);
        }

        if (targetVersion.Status != PlanVersionStatus.Published)
        {
            return Result.Failure<TenantPlanPreviewDto>(TenancyErrors.PlanVersionNotPublished);
        }

        int currentCapacity = tenant.Capacity;
        int targetCapacity = targetVersion.HeadCapacityLimit;
        int deltaCapacity = targetCapacity - currentCapacity;
        bool requiresGracePeriod = currentCapacity > targetCapacity;

        var addedFeatures = new List<string>();
        var removedFeatures = new List<string>();

        if (targetVersion.Features != null)
        {
            foreach (var feature in targetVersion.Features)
            {
                if (feature.IsEnabled)
                {
                    addedFeatures.Add(feature.DisplayName);
                }
            }
        }

        var preview = new TenantPlanPreviewDto(
            TenantId: tenant.Id,
            TenantName: tenant.Name,
            CurrentPlanVersionId: Guid.Empty, // Default or active version if mapped
            CurrentPlanName: tenant.SubscribedPlan,
            CurrentHeadCapacity: currentCapacity,
            CurrentHeadCountUsage: tenant.Capacity,
            TargetPlanVersionId: targetVersion.Id,
            TargetPlanName: targetVersion.VersionName,
            TargetHeadCapacity: targetCapacity,
            DeltaHeadCapacity: deltaCapacity,
            RequiresGracePeriod: requiresGracePeriod,
            GracePeriodDays: requiresGracePeriod ? 14 : 0,
            AddedFeatures: addedFeatures,
            RemovedFeatures: removedFeatures
        );

        return Result.Success(preview);
    }
}
