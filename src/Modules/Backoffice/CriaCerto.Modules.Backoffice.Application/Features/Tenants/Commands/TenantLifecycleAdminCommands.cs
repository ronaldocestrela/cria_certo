using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Features.Tenants;
using CriaCerto.Modules.Backoffice.Application.Features.Tenants.Dtos;
using CriaCerto.Modules.Tenancy.Application.Features.BackofficeTenants;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Backoffice.Application.Features.Tenants.Commands;

public record SuspendTenantAdminCommand(
    Guid TenantId,
    string Reason,
    Guid PerformedByAdminUserId,
    string PerformedByAdminEmail,
    string IpAddress
) : IRequest<Result<TenantAdminDetailDto>>;

public record ReactivateTenantAdminCommand(
    Guid TenantId,
    string Reason,
    Guid PerformedByAdminUserId,
    string PerformedByAdminEmail,
    string IpAddress
) : IRequest<Result<TenantAdminDetailDto>>;

public record CancelTenantAdminCommand(
    Guid TenantId,
    string Reason,
    Guid PerformedByAdminUserId,
    string PerformedByAdminEmail,
    string IpAddress
) : IRequest<Result<TenantAdminDetailDto>>;

public record ArchiveTenantAdminCommand(
    Guid TenantId,
    string Reason,
    Guid PerformedByAdminUserId,
    string PerformedByAdminEmail,
    string IpAddress
) : IRequest<Result<TenantAdminDetailDto>>;

public record SetTenantProtectionAdminCommand(
    Guid TenantId,
    bool IsProtected,
    string Reason,
    Guid PerformedByAdminUserId,
    string PerformedByAdminEmail,
    string IpAddress
) : IRequest<Result<TenantAdminDetailDto>>;

public sealed class SuspendTenantAdminCommandHandler : TenantLifecycleAdminCommandHandlerBase,
    IRequestHandler<SuspendTenantAdminCommand, Result<TenantAdminDetailDto>>
{
    public SuspendTenantAdminCommandHandler(ISender sender, DbContext dbContext) : base(sender, dbContext) { }

    public Task<Result<TenantAdminDetailDto>> Handle(SuspendTenantAdminCommand request, CancellationToken cancellationToken) =>
        HandleLifecycleAsync(
            request.TenantId,
            request.Reason,
            request.PerformedByAdminUserId,
            request.PerformedByAdminEmail,
            request.IpAddress,
            "Tenant.Suspended",
            (id, reason) => new SuspendTenantForAdminCommand(id, reason),
            cancellationToken);
}

public sealed class ReactivateTenantAdminCommandHandler : TenantLifecycleAdminCommandHandlerBase,
    IRequestHandler<ReactivateTenantAdminCommand, Result<TenantAdminDetailDto>>
{
    public ReactivateTenantAdminCommandHandler(ISender sender, DbContext dbContext) : base(sender, dbContext) { }

    public Task<Result<TenantAdminDetailDto>> Handle(ReactivateTenantAdminCommand request, CancellationToken cancellationToken) =>
        HandleLifecycleAsync(
            request.TenantId,
            request.Reason,
            request.PerformedByAdminUserId,
            request.PerformedByAdminEmail,
            request.IpAddress,
            "Tenant.Reactivated",
            (id, reason) => new ReactivateTenantForAdminCommand(id, reason),
            cancellationToken);
}

public sealed class CancelTenantAdminCommandHandler : TenantLifecycleAdminCommandHandlerBase,
    IRequestHandler<CancelTenantAdminCommand, Result<TenantAdminDetailDto>>
{
    public CancelTenantAdminCommandHandler(ISender sender, DbContext dbContext) : base(sender, dbContext) { }

    public Task<Result<TenantAdminDetailDto>> Handle(CancelTenantAdminCommand request, CancellationToken cancellationToken) =>
        HandleLifecycleAsync(
            request.TenantId,
            request.Reason,
            request.PerformedByAdminUserId,
            request.PerformedByAdminEmail,
            request.IpAddress,
            "Tenant.Cancelled",
            (id, reason) => new CancelTenantForAdminCommand(id, reason),
            cancellationToken);
}

public sealed class ArchiveTenantAdminCommandHandler : TenantLifecycleAdminCommandHandlerBase,
    IRequestHandler<ArchiveTenantAdminCommand, Result<TenantAdminDetailDto>>
{
    public ArchiveTenantAdminCommandHandler(ISender sender, DbContext dbContext) : base(sender, dbContext) { }

    public Task<Result<TenantAdminDetailDto>> Handle(ArchiveTenantAdminCommand request, CancellationToken cancellationToken) =>
        HandleLifecycleAsync(
            request.TenantId,
            request.Reason,
            request.PerformedByAdminUserId,
            request.PerformedByAdminEmail,
            request.IpAddress,
            "Tenant.Archived",
            (id, reason) => new ArchiveTenantForAdminCommand(id, reason),
            cancellationToken);
}

public sealed class SetTenantProtectionAdminCommandHandler : TenantLifecycleAdminCommandHandlerBase,
    IRequestHandler<SetTenantProtectionAdminCommand, Result<TenantAdminDetailDto>>
{
    public SetTenantProtectionAdminCommandHandler(ISender sender, DbContext dbContext) : base(sender, dbContext) { }

    public async Task<Result<TenantAdminDetailDto>> Handle(SetTenantProtectionAdminCommand request, CancellationToken cancellationToken)
    {
        var beforeResult = await _sender.Send(new GetTenantBackofficeDetailQuery(request.TenantId), cancellationToken);
        if (beforeResult.IsFailure)
        {
            return Result.Failure<TenantAdminDetailDto>(beforeResult.Error);
        }

        var result = await _sender.Send(
            new SetTenantProtectionForAdminCommand(request.TenantId, request.IsProtected, request.Reason),
            cancellationToken);

        if (result.IsFailure)
        {
            return Result.Failure<TenantAdminDetailDto>(result.Error);
        }

        var after = result.Value;
        var before = beforeResult.Value;

        var auditLog = AuditLog.Create(
            request.PerformedByAdminUserId,
            request.PerformedByAdminEmail,
            "Tenant.ProtectionChanged",
            $"Tenant/{after.Id}",
            request.IpAddress,
            System.Text.Json.JsonSerializer.Serialize(new
            {
                FromIsProtected = before.IsProtected,
                ToIsProtected = after.IsProtected,
                Reason = request.Reason
            }));

        _dbContext.Set<AuditLog>().Add(auditLog);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(TenantAdminMapper.ToDetailDto(after));
    }
}

public abstract class TenantLifecycleAdminCommandHandlerBase
{
    protected readonly ISender _sender;
    protected readonly DbContext _dbContext;

    protected TenantLifecycleAdminCommandHandlerBase(ISender sender, DbContext dbContext)
    {
        _sender = sender;
        _dbContext = dbContext;
    }

    protected async Task<Result<TenantAdminDetailDto>> HandleLifecycleAsync<TCommand>(
        Guid tenantId,
        string reason,
        Guid performedByAdminUserId,
        string performedByAdminEmail,
        string ipAddress,
        string auditAction,
        Func<Guid, string, TCommand> createTenancyCommand,
        CancellationToken cancellationToken)
        where TCommand : IRequest<Result<CriaCerto.Modules.Tenancy.Application.Contracts.TenantBackofficeDetailDto>>
    {
        var beforeResult = await _sender.Send(new GetTenantBackofficeDetailQuery(tenantId), cancellationToken);
        if (beforeResult.IsFailure)
        {
            return Result.Failure<TenantAdminDetailDto>(beforeResult.Error);
        }

        var result = await _sender.Send(createTenancyCommand(tenantId, reason), cancellationToken);
        if (result.IsFailure)
        {
            return Result.Failure<TenantAdminDetailDto>(result.Error);
        }

        var after = result.Value;
        var before = beforeResult.Value;

        var auditLog = AuditLog.Create(
            performedByAdminUserId,
            performedByAdminEmail,
            auditAction,
            $"Tenant/{after.Id}",
            ipAddress,
            System.Text.Json.JsonSerializer.Serialize(new
            {
                FromStatus = before.Status,
                ToStatus = after.Status,
                Reason = reason,
                IsProtected = after.IsProtected
            }));

        _dbContext.Set<AuditLog>().Add(auditLog);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(TenantAdminMapper.ToDetailDto(after));
    }
}
