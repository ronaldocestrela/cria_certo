using System.Text.Json;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Backoffice.Application.Features.Impersonation.Commands;

public record StopImpersonationSessionCommand(
    Guid SessionId,
    Guid AdminUserId,
    string AdminUserEmail,
    string IpAddress,
    bool IsPlatformOwner = false) : IRequest<Result>;

public class StopImpersonationSessionCommandHandler : IRequestHandler<StopImpersonationSessionCommand, Result>
{
    private readonly DbContext _dbContext;

    public StopImpersonationSessionCommandHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result> Handle(
        StopImpersonationSessionCommand command,
        CancellationToken cancellationToken)
    {
        var session = await _dbContext.Set<ImpersonationSession>()
            .FirstOrDefaultAsync(s => s.Id == command.SessionId, cancellationToken);

        if (session is null)
        {
            return Result.Failure(
                Error.NotFound("Impersonation.SessionNotFound", "Sessão de impersonação não encontrada."));
        }

        // Authorization check: Operator must be the one who started it or PlatformOwner
        if (session.AdminUserId != command.AdminUserId && !command.IsPlatformOwner)
        {
            return Result.Failure(
                Error.Unauthorized("Impersonation.Unauthorized", "Apenas o operador responsável ou PlatformOwner pode encerrar esta sessão."));
        }

        if (session.Status != ImpersonationSessionStatus.Active)
        {
            // Already stopped or revoked - idempotent success
            return Result.Success();
        }

        session.End();

        var auditDetails = JsonSerializer.Serialize(new
        {
            SessionId = session.Id,
            TargetTenantId = session.TargetTenantId,
            TargetTenantName = session.TargetTenantName,
            SupportTicket = session.SupportTicket,
            StartedAtUtc = session.StartedAtUtc,
            EndedAtUtc = session.EndedAtUtc,
            ClosedByAdminId = command.AdminUserId,
            ClosedByAdminEmail = command.AdminUserEmail
        });

        var auditLog = AuditLog.Create(
            command.AdminUserId,
            command.AdminUserEmail,
            "Impersonation.Stopped",
            $"Tenant/{session.TargetTenantId}",
            command.IpAddress,
            auditDetails);

        _dbContext.Set<AuditLog>().Add(auditLog);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
