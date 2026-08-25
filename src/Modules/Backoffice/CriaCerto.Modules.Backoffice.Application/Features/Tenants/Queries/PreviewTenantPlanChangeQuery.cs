using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;
using CriaCerto.Modules.Backoffice.Application.Features.Tenants.Dtos;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Domain.Errors;
using MediatR;

namespace CriaCerto.Modules.Backoffice.Application.Features.Tenants.Queries;

public sealed record PreviewTenantPlanChangeQuery(
    Guid TenantId,
    Guid TargetPlanVersionId
) : IRequest<Result<TenantPlanPreviewDto>>;

public sealed class PreviewTenantPlanChangeQueryHandler : IRequestHandler<PreviewTenantPlanChangeQuery, Result<TenantPlanPreviewDto>>
{
    private readonly Func<Guid, Task<Tenant?>> _tenantLookup;
    private readonly Func<Guid, Task<PlanVersion?>> _planVersionLookup;

    public PreviewTenantPlanChangeQueryHandler(
        Func<Guid, Task<Tenant?>> tenantLookup,
        Func<Guid, Task<PlanVersion?>> planVersionLookup)
    {
        _tenantLookup = tenantLookup;
        _planVersionLookup = planVersionLookup;
    }

    public async Task<Result<TenantPlanPreviewDto>> Handle(PreviewTenantPlanChangeQuery request, CancellationToken cancellationToken)
    {
        var tenant = await _tenantLookup(request.TenantId);
        if (tenant is null)
        {
            return Result.Failure<TenantPlanPreviewDto>(TenancyErrors.TenantNotFound);
        }

        var targetVersion = await _planVersionLookup(request.TargetPlanVersionId);
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

        foreach (var feature in targetVersion.Features)
        {
            if (feature.IsEnabled)
            {
                addedFeatures.Add(feature.DisplayName);
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
