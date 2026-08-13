using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Backoffice.Application.Features.AdminUsers.Commands;

public record ToggleAdminUserStatusCommand(
    Guid Id,
    bool IsActive,
    Guid PerformedByAdminUserId,
    string PerformedByAdminEmail,
    string IpAddress
) : IRequest<Result>;

public class ToggleAdminUserStatusCommandHandler : IRequestHandler<ToggleAdminUserStatusCommand, Result>
{
    private readonly DbContext _dbContext;

    public ToggleAdminUserStatusCommandHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result> Handle(ToggleAdminUserStatusCommand request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Set<AdminUser>()
            .FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken);

        if (user is null)
        {
            return Result.Failure(BackofficeErrors.UserNotFound);
        }

        Result result = request.IsActive ? user.Activate() : user.Deactivate();
        if (result.IsFailure)
        {
            return result;
        }

        // If deactivated, revoke all active sessions immediately
        if (!request.IsActive)
        {
            var activeSessions = await _dbContext.Set<AdminSession>()
                .Where(s => s.AdminUserId == user.Id && !s.IsRevoked)
                .ToListAsync(cancellationToken);

            foreach (var session in activeSessions)
            {
                session.Revoke();
            }
        }

        var actionName = request.IsActive ? "AdminUser.Activated" : "AdminUser.Deactivated";
        var auditLog = AuditLog.Create(
            request.PerformedByAdminUserId,
            request.PerformedByAdminEmail,
            actionName,
            $"AdminUser/{user.Id}",
            request.IpAddress,
            System.Text.Json.JsonSerializer.Serialize(new { user.Id, user.Email, request.IsActive }));

        _dbContext.Set<AuditLog>().Add(auditLog);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
