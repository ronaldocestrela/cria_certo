using System.Text.Json;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Features.Tenants.Dtos;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Domain.Errors;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CriaCerto.Modules.Backoffice.Application.Features.Tenants.Commands;

public sealed record ChangeTenantPlanCommand(
    Guid TenantId,
    Guid? TargetPlanVersionId,
    Guid AdminUserId,
    string Justification,
    bool ForceImmediate = false,
    string? HeadTierCode = null,
    IReadOnlyCollection<string>? SubscribedModules = null,
    string? AdminEmail = null,
    string? IpAddress = null
) : IRequest<Result<ChangeTenantPlanResponseDto>>;

public sealed class ChangeTenantPlanCommandValidator : AbstractValidator<ChangeTenantPlanCommand>
{
    public ChangeTenantPlanCommandValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("O ID do tenant é obrigatório.");

        RuleFor(x => x.AdminUserId)
            .NotEmpty().WithMessage("O ID do administrador é obrigatório.");

        RuleFor(x => x.Justification)
            .NotEmpty().WithMessage("A justificativa é obrigatória.")
            .MinimumLength(15).WithMessage("A justificativa deve conter no mínimo 15 caracteres.");

        RuleFor(x => x)
            .Must(x => (x.TargetPlanVersionId.HasValue && x.TargetPlanVersionId.Value != Guid.Empty)
                       || !string.IsNullOrWhiteSpace(x.HeadTierCode)
                       || x.SubscribedModules != null)
            .WithMessage("É necessário informar uma versão de plano ou uma configuração modular (faixa de cabeças / módulos).");

        When(x => !string.IsNullOrWhiteSpace(x.HeadTierCode), () =>
        {
            RuleFor(x => x.HeadTierCode!)
                .Must(HeadTierCapacityLimits.IsValidHeadTierCode)
                .WithMessage("A faixa de cabeças informada é inválida.");
        });
    }
}

public sealed class ChangeTenantPlanCommandHandler : IRequestHandler<ChangeTenantPlanCommand, Result<ChangeTenantPlanResponseDto>>
{
    private readonly ITenancyDbContext? _tenancyDbContext;
    private readonly DbContext? _backofficeDbContext;
    private readonly Func<Guid, Task<Tenant?>>? _tenantLookup;
    private readonly Func<Guid, Task<PlanVersion?>>? _planVersionLookup;
    private readonly Func<TenantSubscriptionHistory, Task>? _saveHistory;

    [ActivatorUtilitiesConstructor]
    public ChangeTenantPlanCommandHandler(
        ITenancyDbContext tenancyDbContext,
        DbContext backofficeDbContext)
    {
        _tenancyDbContext = tenancyDbContext;
        _backofficeDbContext = backofficeDbContext;
    }

    public ChangeTenantPlanCommandHandler(
        Func<Guid, Task<Tenant?>> tenantLookup,
        Func<Guid, Task<PlanVersion?>> planVersionLookup,
        Func<TenantSubscriptionHistory, Task> saveHistory)
    {
        _tenantLookup = tenantLookup;
        _planVersionLookup = planVersionLookup;
        _saveHistory = saveHistory;
    }

    public async Task<Result<ChangeTenantPlanResponseDto>> Handle(ChangeTenantPlanCommand request, CancellationToken cancellationToken)
    {
        Tenant? tenant = _tenantLookup != null
            ? await _tenantLookup(request.TenantId)
            : await _tenancyDbContext!.Tenants
                .Include(t => t.SubscribedModules)
                .FirstOrDefaultAsync(t => t.Id == request.TenantId, cancellationToken);

        if (tenant is null)
        {
            return Result.Failure<ChangeTenantPlanResponseDto>(TenancyErrors.TenantNotFound);
        }

        // --- Modular Self-Service Flow ---
        if (!string.IsNullOrWhiteSpace(request.HeadTierCode) || request.SubscribedModules != null)
        {
            var targetTierCode = !string.IsNullOrWhiteSpace(request.HeadTierCode)
                ? HeadTierCapacityLimits.NormalizeHeadTierCode(request.HeadTierCode)
                : (string.IsNullOrWhiteSpace(tenant.HeadTierCode) ? PlanCapacityLimits.Tier500 : tenant.HeadTierCode);

            int targetCapacity = HeadTierCapacityLimits.GetMaxHeadsForTier(targetTierCode);
            bool usageExceeds = tenant.Capacity > targetCapacity;
            bool isGracePeriodActivated = usageExceeds && !request.ForceImmediate;

            DateTime? gracePeriodEnds = isGracePeriodActivated ? DateTime.UtcNow.AddDays(14) : null;
            string statusText = isGracePeriodActivated ? "GracePeriodActive" : "Active";

            var targetModules = request.SubscribedModules != null
                ? request.SubscribedModules
                    .Where(m => !string.IsNullOrWhiteSpace(m))
                    .Select(m => m.Trim())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase)
                : tenant.GetSubscribedModuleKeys().ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Core Cows module is strictly mandatory
            targetModules.Add("Cows");

            if (!isGracePeriodActivated)
            {
                tenant.HeadTierCode = targetTierCode;
                tenant.Capacity = targetCapacity;
                tenant.SubscribedPlan = $"{targetTierCode} Modular";
                tenant.UpdatedAtUtc = DateTime.UtcNow;
                tenant.SetSubscribedModules(targetModules);
            }

            var actionType = isGracePeriodActivated
                ? SubscriptionActionType.DowngradeGracePeriodStarted
                : (usageExceeds ? SubscriptionActionType.DowngradeImmediate : SubscriptionActionType.PlanChanged);

            var history = TenantSubscriptionHistory.Create(
                tenantId: tenant.Id,
                previousPlanVersionId: null,
                newPlanVersionId: Guid.Empty,
                changedByAdminUserId: request.AdminUserId,
                justification: request.Justification.Trim(),
                actionType: actionType,
                snapshotHeadCount: tenant.Capacity,
                snapshotUserCount: 1,
                snapshotUnitCount: tenant.ProductionUnits?.Count ?? 0
            );

            if (_saveHistory != null)
            {
                await _saveHistory(history);
            }
            else if (_tenancyDbContext != null)
            {
                _tenancyDbContext.SubscriptionHistories.Add(history);
                await _tenancyDbContext.SaveChangesAsync(cancellationToken);
            }

            if (_backofficeDbContext != null)
            {
                var auditLog = AuditLog.Create(
                    request.AdminUserId,
                    request.AdminEmail ?? "admin@criacerto.com.br",
                    "TENANT_MODULAR_PLAN_CHANGED",
                    $"Tenant/{tenant.Id}",
                    request.IpAddress ?? "127.0.0.1",
                    JsonSerializer.Serialize(new
                    {
                        TenantId = tenant.Id,
                        HeadTierCode = targetTierCode,
                        HeadCapacity = targetCapacity,
                        SubscribedModules = targetModules.ToList(),
                        IsGracePeriodActivated = isGracePeriodActivated,
                        Justification = request.Justification
                    })
                );
                _backofficeDbContext.Set<AuditLog>().Add(auditLog);
                await _backofficeDbContext.SaveChangesAsync(cancellationToken);
            }

            string message = isGracePeriodActivated
                ? "Grace Period de 14 dias ativado para adequação do uso do tenant aos limites do novo plano modular."
                : $"Plano modular do tenant alterado com sucesso ({targetTierCode}, {targetModules.Count} módulos).";

            var response = new ChangeTenantPlanResponseDto(
                TenantId: tenant.Id,
                AppliedPlanVersionId: Guid.Empty,
                PlanName: $"{targetTierCode} Modular",
                SubscriptionStatus: statusText,
                GracePeriodActivated: isGracePeriodActivated,
                GracePeriodEndsAtUtc: gracePeriodEnds,
                Message: message,
                HeadTierCode: targetTierCode,
                SubscribedModules: targetModules.ToList(),
                HeadCapacity: targetCapacity
            );

            return Result.Success(response);
        }

        // --- Legacy Plan Version Flow ---
        var targetPlanVersionId = request.TargetPlanVersionId ?? Guid.Empty;
        PlanVersion? targetVersion = _planVersionLookup != null
            ? await _planVersionLookup(targetPlanVersionId)
            : await _backofficeDbContext!.Set<PlanVersion>().FirstOrDefaultAsync(v => v.Id == targetPlanVersionId, cancellationToken);

        if (targetVersion is null)
        {
            return Result.Failure<ChangeTenantPlanResponseDto>(TenancyErrors.PlanVersionNotFound);
        }

        if (targetVersion.Status != PlanVersionStatus.Published)
        {
            return Result.Failure<ChangeTenantPlanResponseDto>(TenancyErrors.PlanVersionNotPublished);
        }

        bool usageExceedsLegacy = tenant.Capacity > targetVersion.HeadCapacityLimit;
        bool isGracePeriodActivatedLegacy = usageExceedsLegacy && !request.ForceImmediate;

        DateTime? gracePeriodEndsLegacy = isGracePeriodActivatedLegacy ? DateTime.UtcNow.AddDays(14) : null;
        string statusTextLegacy = isGracePeriodActivatedLegacy ? "GracePeriodActive" : "Active";

        if (!isGracePeriodActivatedLegacy)
        {
            string planNameToSet = targetVersion.VersionName;
            if (_backofficeDbContext != null)
            {
                var catalog = await _backofficeDbContext.Set<PlanCatalog>()
                    .FirstOrDefaultAsync(c => c.Id == targetVersion.PlanCatalogId, cancellationToken);
                if (catalog != null && !planNameToSet.Contains(catalog.Name, StringComparison.OrdinalIgnoreCase)
                    && !planNameToSet.Contains("Starter", StringComparison.OrdinalIgnoreCase)
                    && !planNameToSet.Contains("Pro", StringComparison.OrdinalIgnoreCase)
                    && !planNameToSet.Contains("Enterprise", StringComparison.OrdinalIgnoreCase))
                {
                    planNameToSet = $"{catalog.Name} - {targetVersion.VersionName}";
                }
            }

            tenant.SubscribedPlan = planNameToSet;
            tenant.Capacity = targetVersion.HeadCapacityLimit;
            tenant.UpdatedAtUtc = DateTime.UtcNow;
        }

        var actionTypeLegacy = isGracePeriodActivatedLegacy
            ? SubscriptionActionType.DowngradeGracePeriodStarted
            : (usageExceedsLegacy ? SubscriptionActionType.DowngradeImmediate : SubscriptionActionType.Upgrade);

        var historyLegacy = TenantSubscriptionHistory.Create(
            tenantId: tenant.Id,
            previousPlanVersionId: null,
            newPlanVersionId: targetVersion.Id,
            changedByAdminUserId: request.AdminUserId,
            justification: request.Justification.Trim(),
            actionType: actionTypeLegacy,
            snapshotHeadCount: tenant.Capacity,
            snapshotUserCount: 1,
            snapshotUnitCount: tenant.ProductionUnits?.Count ?? 0
        );

        if (_saveHistory != null)
        {
            await _saveHistory(historyLegacy);
        }
        else if (_tenancyDbContext != null)
        {
            _tenancyDbContext.SubscriptionHistories.Add(historyLegacy);
            await _tenancyDbContext.SaveChangesAsync(cancellationToken);
        }

        string messageLegacy = isGracePeriodActivatedLegacy
            ? "Grace Period de 14 dias ativado para adequação do uso do tenant aos limites do novo plano."
            : $"Plano do tenant alterado com sucesso para {targetVersion.VersionName}.";

        var responseLegacy = new ChangeTenantPlanResponseDto(
            TenantId: tenant.Id,
            AppliedPlanVersionId: targetVersion.Id,
            PlanName: targetVersion.VersionName,
            SubscriptionStatus: statusTextLegacy,
            GracePeriodActivated: isGracePeriodActivatedLegacy,
            GracePeriodEndsAtUtc: gracePeriodEndsLegacy,
            Message: messageLegacy
        );

        return Result.Success(responseLegacy);
    }
}
