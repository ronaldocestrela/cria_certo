using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Backoffice.Application.Features.AdminUsers.Commands;

public record ResetAdminUserPasswordCommand(
    Guid Id,
    string NewRawPassword,
    Guid PerformedByAdminUserId,
    string PerformedByAdminEmail,
    string IpAddress
) : IRequest<Result>;

public class ResetAdminUserPasswordCommandHandler : IRequestHandler<ResetAdminUserPasswordCommand, Result>
{
    private readonly DbContext _dbContext;

    public ResetAdminUserPasswordCommandHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result> Handle(ResetAdminUserPasswordCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.NewRawPassword) || request.NewRawPassword.Length < 6)
        {
            return Result.Failure(BackofficeErrors.WeakPassword);
        }

        var user = await _dbContext.Set<AdminUser>()
            .FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken);

        if (user is null)
        {
            return Result.Failure(BackofficeErrors.UserNotFound);
        }

        string newHash = $"hash_{request.NewRawPassword}";
        var updateResult = user.UpdatePasswordHash(newHash);
        if (updateResult.IsFailure)
        {
            return updateResult;
        }

        // Revoke all existing sessions on password reset for security
        var activeSessions = await _dbContext.Set<AdminSession>()
            .Where(s => s.AdminUserId == user.Id && !s.IsRevoked)
            .ToListAsync(cancellationToken);

        foreach (var session in activeSessions)
        {
            session.Revoke();
        }

        var auditLog = AuditLog.Create(
            request.PerformedByAdminUserId,
            request.PerformedByAdminEmail,
            "AdminUser.PasswordReset",
            $"AdminUser/{user.Id}",
            request.IpAddress);

        _dbContext.Set<AuditLog>().Add(auditLog);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
