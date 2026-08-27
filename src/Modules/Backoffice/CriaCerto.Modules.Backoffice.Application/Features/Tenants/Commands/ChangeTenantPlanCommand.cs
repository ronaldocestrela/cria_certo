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
    Guid TargetPlanVersionId,
    Guid AdminUserId,
    string Justification,
    bool ForceImmediate = false
) : IRequest<Result<ChangeTenantPlanResponseDto>>;

public sealed class ChangeTenantPlanCommandValidator : AbstractValidator<ChangeTenantPlanCommand>
{
    public ChangeTenantPlanCommandValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("O ID do tenant é obrigatório.");

        RuleFor(x => x.TargetPlanVersionId)
            .NotEmpty().WithMessage("A versão do plano de destino é obrigatória.");

        RuleFor(x => x.AdminUserId)
            .NotEmpty().WithMessage("O ID do administrador é obrigatório.");

        RuleFor(x => x.Justification)
            .NotEmpty().WithMessage("A justificativa é obrigatória.")
            .MinimumLength(15).WithMessage("A justificativa deve conter no mínimo 15 caracteres.");
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
            : await _tenancyDbContext!.Tenants.FirstOrDefaultAsync(t => t.Id == request.TenantId, cancellationToken);

        if (tenant is null)
        {
            return Result.Failure<ChangeTenantPlanResponseDto>(TenancyErrors.TenantNotFound);
        }

        PlanVersion? targetVersion = _planVersionLookup != null
            ? await _planVersionLookup(request.TargetPlanVersionId)
            : await _backofficeDbContext!.Set<PlanVersion>().FirstOrDefaultAsync(v => v.Id == request.TargetPlanVersionId, cancellationToken);

        if (targetVersion is null)
        {
            return Result.Failure<ChangeTenantPlanResponseDto>(TenancyErrors.PlanVersionNotFound);
        }

        if (targetVersion.Status != PlanVersionStatus.Published)
        {
            return Result.Failure<ChangeTenantPlanResponseDto>(TenancyErrors.PlanVersionNotPublished);
        }

        bool usageExceeds = tenant.Capacity > targetVersion.HeadCapacityLimit;
        bool isGracePeriodActivated = usageExceeds && !request.ForceImmediate;

        DateTime? gracePeriodEnds = isGracePeriodActivated ? DateTime.UtcNow.AddDays(14) : null;
        string statusText = isGracePeriodActivated ? "GracePeriodActive" : "Active";

        if (!isGracePeriodActivated)
        {
            tenant.SubscribedPlan = targetVersion.VersionName;
            tenant.Capacity = targetVersion.HeadCapacityLimit;
            tenant.UpdatedAtUtc = DateTime.UtcNow;
        }

        var actionType = isGracePeriodActivated
            ? SubscriptionActionType.DowngradeGracePeriodStarted
            : (usageExceeds ? SubscriptionActionType.DowngradeImmediate : SubscriptionActionType.Upgrade);

        var history = TenantSubscriptionHistory.Create(
            tenantId: tenant.Id,
            previousPlanVersionId: null,
            newPlanVersionId: targetVersion.Id,
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

        string message = isGracePeriodActivated
            ? "Grace Period de 14 dias ativado para adequação do uso do tenant aos limites do novo plano."
            : $"Plano do tenant alterado com sucesso para {targetVersion.VersionName}.";

        var response = new ChangeTenantPlanResponseDto(
            TenantId: tenant.Id,
            AppliedPlanVersionId: targetVersion.Id,
            PlanName: targetVersion.VersionName,
            SubscriptionStatus: statusText,
            GracePeriodActivated: isGracePeriodActivated,
            GracePeriodEndsAtUtc: gracePeriodEnds,
            Message: message
        );

        return Result.Success(response);
    }
}
