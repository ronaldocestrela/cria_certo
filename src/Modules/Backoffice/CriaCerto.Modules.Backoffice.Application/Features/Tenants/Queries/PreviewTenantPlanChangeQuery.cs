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
    Guid? TargetPlanVersionId = null,
    string? HeadTierCode = null,
    IReadOnlyCollection<string>? SelectedModules = null
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
            : await _tenancyDbContext!.Tenants
                .Include(t => t.SubscribedModules)
                .FirstOrDefaultAsync(t => t.Id == request.TenantId, cancellationToken);

        if (tenant is null)
        {
            return Result.Failure<TenantPlanPreviewDto>(TenancyErrors.TenantNotFound);
        }

        // Modular flow
        if (!string.IsNullOrWhiteSpace(request.HeadTierCode) || request.SelectedModules != null)
        {
            var targetTierCode = !string.IsNullOrWhiteSpace(request.HeadTierCode)
                ? (HeadTierCapacityLimits.IsValidHeadTierCode(request.HeadTierCode)
                    ? HeadTierCapacityLimits.NormalizeHeadTierCode(request.HeadTierCode)
                    : tenant.HeadTierCode)
                : tenant.HeadTierCode;

            var targetCapacity = HeadTierCapacityLimits.GetMaxHeadsForTier(targetTierCode);
            var deltaCapacity = targetCapacity - tenant.Capacity;
            var requiresGracePeriod = tenant.Capacity > targetCapacity;

            var currentModules = tenant.GetSubscribedModuleKeys();
            var targetModulesSet = request.SelectedModules != null
                ? request.SelectedModules
                    .Where(m => !string.IsNullOrWhiteSpace(m))
                    .Select(m => m.Trim())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase)
                : currentModules.ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Core module Cows is always included
            targetModulesSet.Add("Cows");

            var addedFeatures = targetModulesSet
                .Where(m => !currentModules.Contains(m, StringComparer.OrdinalIgnoreCase))
                .ToList();

            var removedFeatures = currentModules
                .Where(m => !targetModulesSet.Contains(m) && !string.Equals(m, "Cows", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var preview = new TenantPlanPreviewDto(
                TenantId: tenant.Id,
                TenantName: tenant.Name,
                CurrentPlanVersionId: Guid.Empty,
                CurrentPlanName: tenant.SubscribedPlan,
                CurrentHeadCapacity: tenant.Capacity,
                CurrentHeadCountUsage: tenant.Capacity,
                TargetPlanVersionId: Guid.Empty,
                TargetPlanName: $"{targetTierCode} Modular",
                TargetHeadCapacity: targetCapacity,
                DeltaHeadCapacity: deltaCapacity,
                RequiresGracePeriod: requiresGracePeriod,
                GracePeriodDays: requiresGracePeriod ? 14 : 0,
                AddedFeatures: addedFeatures,
                RemovedFeatures: removedFeatures,
                CurrentHeadTierCode: tenant.HeadTierCode,
                TargetHeadTierCode: targetTierCode,
                CurrentSubscribedModules: currentModules.ToList(),
                TargetSubscribedModules: targetModulesSet.ToList()
            );

            return Result.Success(preview);
        }

        // Legacy plan version flow
        var targetPlanVersionId = request.TargetPlanVersionId ?? Guid.Empty;
        PlanVersion? targetVersion = _planVersionLookup != null
            ? await _planVersionLookup(targetPlanVersionId)
            : await _backofficeDbContext!.Set<PlanVersion>()
                .Include(v => v.Features)
                .FirstOrDefaultAsync(v => v.Id == targetPlanVersionId, cancellationToken);

        if (targetVersion is null)
        {
            return Result.Failure<TenantPlanPreviewDto>(TenancyErrors.PlanVersionNotFound);
        }

        if (targetVersion.Status != PlanVersionStatus.Published)
        {
            return Result.Failure<TenantPlanPreviewDto>(TenancyErrors.PlanVersionNotPublished);
        }

        int currentCapacity = tenant.Capacity;
        int targetCapacityLegacy = targetVersion.HeadCapacityLimit;
        int deltaCapacityLegacy = targetCapacityLegacy - currentCapacity;
        bool requiresGracePeriodLegacy = currentCapacity > targetCapacityLegacy;

        var addedFeaturesLegacy = new List<string>();
        var removedFeaturesLegacy = new List<string>();

        if (targetVersion.Features != null)
        {
            foreach (var feature in targetVersion.Features)
            {
                if (feature.IsEnabled)
                {
                    addedFeaturesLegacy.Add(feature.DisplayName);
                }
            }
        }

        var previewLegacy = new TenantPlanPreviewDto(
            TenantId: tenant.Id,
            TenantName: tenant.Name,
            CurrentPlanVersionId: Guid.Empty,
            CurrentPlanName: tenant.SubscribedPlan,
            CurrentHeadCapacity: currentCapacity,
            CurrentHeadCountUsage: tenant.Capacity,
            TargetPlanVersionId: targetVersion.Id,
            TargetPlanName: targetVersion.VersionName,
            TargetHeadCapacity: targetCapacityLegacy,
            DeltaHeadCapacity: deltaCapacityLegacy,
            RequiresGracePeriod: requiresGracePeriodLegacy,
            GracePeriodDays: requiresGracePeriodLegacy ? 14 : 0,
            AddedFeatures: addedFeaturesLegacy,
            RemovedFeatures: removedFeaturesLegacy
        );

        return Result.Success(previewLegacy);
    }
}
