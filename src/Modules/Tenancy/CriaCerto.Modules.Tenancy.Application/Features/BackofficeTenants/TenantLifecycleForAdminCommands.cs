using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Contracts;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Domain.Errors;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Tenancy.Application.Features.BackofficeTenants;

public record SuspendTenantForAdminCommand(Guid TenantId, string Reason)
    : IRequest<Result<TenantBackofficeDetailDto>>;

public record ReactivateTenantForAdminCommand(Guid TenantId, string Reason)
    : IRequest<Result<TenantBackofficeDetailDto>>;

public record CancelTenantForAdminCommand(Guid TenantId, string Reason)
    : IRequest<Result<TenantBackofficeDetailDto>>;

public record ArchiveTenantForAdminCommand(Guid TenantId, string Reason)
    : IRequest<Result<TenantBackofficeDetailDto>>;

public record SetTenantProtectionForAdminCommand(Guid TenantId, bool IsProtected, string Reason)
    : IRequest<Result<TenantBackofficeDetailDto>>;

public sealed class TenantLifecycleReasonValidator : AbstractValidator<SuspendTenantForAdminCommand>
{
    public TenantLifecycleReasonValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().WithMessage("O ID do tenant é obrigatório.");
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("A justificativa é obrigatória.")
            .MinimumLength(TenantLifecycle.MinJustificationLength)
            .WithMessage($"A justificativa deve conter no mínimo {TenantLifecycle.MinJustificationLength} caracteres.");
    }
}

public sealed class ReactivateTenantForAdminCommandValidator : AbstractValidator<ReactivateTenantForAdminCommand>
{
    public ReactivateTenantForAdminCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().WithMessage("O ID do tenant é obrigatório.");
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("A justificativa é obrigatória.")
            .MinimumLength(TenantLifecycle.MinJustificationLength)
            .WithMessage($"A justificativa deve conter no mínimo {TenantLifecycle.MinJustificationLength} caracteres.");
    }
}

public sealed class CancelTenantForAdminCommandValidator : AbstractValidator<CancelTenantForAdminCommand>
{
    public CancelTenantForAdminCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().WithMessage("O ID do tenant é obrigatório.");
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("A justificativa é obrigatória.")
            .MinimumLength(TenantLifecycle.MinJustificationLength)
            .WithMessage($"A justificativa deve conter no mínimo {TenantLifecycle.MinJustificationLength} caracteres.");
    }
}

public sealed class ArchiveTenantForAdminCommandValidator : AbstractValidator<ArchiveTenantForAdminCommand>
{
    public ArchiveTenantForAdminCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().WithMessage("O ID do tenant é obrigatório.");
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("A justificativa é obrigatória.")
            .MinimumLength(TenantLifecycle.MinJustificationLength)
            .WithMessage($"A justificativa deve conter no mínimo {TenantLifecycle.MinJustificationLength} caracteres.");
    }
}

public sealed class SetTenantProtectionForAdminCommandValidator : AbstractValidator<SetTenantProtectionForAdminCommand>
{
    public SetTenantProtectionForAdminCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().WithMessage("O ID do tenant é obrigatório.");
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("A justificativa é obrigatória.")
            .MinimumLength(TenantLifecycle.MinJustificationLength)
            .WithMessage($"A justificativa deve conter no mínimo {TenantLifecycle.MinJustificationLength} caracteres.");
    }
}

public sealed class SuspendTenantForAdminCommandHandler : IRequestHandler<SuspendTenantForAdminCommand, Result<TenantBackofficeDetailDto>>
{
    private readonly ITenancyDbContext _dbContext;

    public SuspendTenantForAdminCommandHandler(ITenancyDbContext dbContext) => _dbContext = dbContext;

    public Task<Result<TenantBackofficeDetailDto>> Handle(SuspendTenantForAdminCommand request, CancellationToken cancellationToken) =>
        TenantLifecycleCommandHelper.ApplyLifecycleChange(
            _dbContext, request.TenantId, tenant => tenant.Suspend(request.Reason), cancellationToken);
}

public sealed class ReactivateTenantForAdminCommandHandler : IRequestHandler<ReactivateTenantForAdminCommand, Result<TenantBackofficeDetailDto>>
{
    private readonly ITenancyDbContext _dbContext;

    public ReactivateTenantForAdminCommandHandler(ITenancyDbContext dbContext) => _dbContext = dbContext;

    public Task<Result<TenantBackofficeDetailDto>> Handle(ReactivateTenantForAdminCommand request, CancellationToken cancellationToken) =>
        TenantLifecycleCommandHelper.ApplyLifecycleChange(
            _dbContext, request.TenantId, tenant => tenant.Reactivate(request.Reason), cancellationToken);
}

public sealed class CancelTenantForAdminCommandHandler : IRequestHandler<CancelTenantForAdminCommand, Result<TenantBackofficeDetailDto>>
{
    private readonly ITenancyDbContext _dbContext;

    public CancelTenantForAdminCommandHandler(ITenancyDbContext dbContext) => _dbContext = dbContext;

    public Task<Result<TenantBackofficeDetailDto>> Handle(CancelTenantForAdminCommand request, CancellationToken cancellationToken) =>
        TenantLifecycleCommandHelper.ApplyLifecycleChange(
            _dbContext, request.TenantId, tenant => tenant.Cancel(request.Reason), cancellationToken);
}

public sealed class ArchiveTenantForAdminCommandHandler : IRequestHandler<ArchiveTenantForAdminCommand, Result<TenantBackofficeDetailDto>>
{
    private readonly ITenancyDbContext _dbContext;

    public ArchiveTenantForAdminCommandHandler(ITenancyDbContext dbContext) => _dbContext = dbContext;

    public Task<Result<TenantBackofficeDetailDto>> Handle(ArchiveTenantForAdminCommand request, CancellationToken cancellationToken) =>
        TenantLifecycleCommandHelper.ApplyLifecycleChange(
            _dbContext, request.TenantId, tenant => tenant.Archive(request.Reason), cancellationToken);
}

public sealed class SetTenantProtectionForAdminCommandHandler : IRequestHandler<SetTenantProtectionForAdminCommand, Result<TenantBackofficeDetailDto>>
{
    private readonly ITenancyDbContext _dbContext;

    public SetTenantProtectionForAdminCommandHandler(ITenancyDbContext dbContext) => _dbContext = dbContext;

    public Task<Result<TenantBackofficeDetailDto>> Handle(SetTenantProtectionForAdminCommand request, CancellationToken cancellationToken) =>
        TenantLifecycleCommandHelper.ApplyLifecycleChange(
            _dbContext, request.TenantId, tenant => tenant.SetProtection(request.IsProtected, request.Reason), cancellationToken);
}

internal static class TenantLifecycleCommandHelper
{
    public static async Task<Result<TenantBackofficeDetailDto>> ApplyLifecycleChange(
        ITenancyDbContext dbContext,
        Guid tenantId,
        Func<Tenant, Result> applyChange,
        CancellationToken cancellationToken)
    {
        var tenant = await dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

        if (tenant is null)
        {
            return Result.Failure<TenantBackofficeDetailDto>(TenancyErrors.TenantNotFound);
        }

        var changeResult = applyChange(tenant);
        if (changeResult.IsFailure)
        {
            return Result.Failure<TenantBackofficeDetailDto>(changeResult.Error);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var teamCount = await dbContext.UserTenants.CountAsync(ut => ut.TenantId == tenant.Id, cancellationToken);
        var unitCount = await dbContext.ProductionUnits.CountAsync(pu => pu.TenantId == tenant.Id, cancellationToken);

        return Result.Success(TenantBackofficeMapper.ToDetailDto(tenant, teamCount, unitCount));
    }
}
