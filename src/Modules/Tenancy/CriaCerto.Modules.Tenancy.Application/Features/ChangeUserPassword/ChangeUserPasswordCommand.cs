using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Tenancy.Application.Features.ChangeUserPassword;

public record ChangeUserPasswordRequest(string CurrentPassword, string NewPassword, string ConfirmNewPassword);

public record ChangeUserPasswordCommand(
    Guid UserId,
    string CurrentPassword,
    string NewPassword,
    string ConfirmNewPassword,
    string? ClientIpAddress = null
) : IRequest<Result>;

public sealed class ChangeUserPasswordCommandHandler : IRequestHandler<ChangeUserPasswordCommand, Result>
{
    private readonly ITenancyDbContext _dbContext;
    private readonly ITenancyAuditSink? _auditSink;

    public ChangeUserPasswordCommandHandler(
        ITenancyDbContext dbContext,
        ITenancyAuditSink? auditSink = null)
    {
        _dbContext = dbContext;
        _auditSink = auditSink;
    }

    public async Task<Result> Handle(ChangeUserPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (user == null)
        {
            return Result.Failure(
                Error.NotFound("User.NotFound", "Usuário não encontrado."));
        }

        if (!PasswordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            return Result.Failure(
                Error.Validation("Auth.InvalidCurrentPassword", "A senha atual informada está incorreta."));
        }

        if (PasswordHasher.Verify(request.NewPassword, user.PasswordHash))
        {
            return Result.Failure(
                Error.Validation("Auth.SamePassword", "A nova senha não pode ser igual à senha atual."));
        }

        var newPasswordHash = PasswordHasher.Hash(request.NewPassword);
        var changeResult = user.ChangePassword(newPasswordHash);
        if (changeResult.IsFailure)
        {
            return Result.Failure(changeResult.Error);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (_auditSink != null)
        {
            await _auditSink.RecordUserPasswordChangeAuditAsync(
                user.Id,
                user.Email,
                request.ClientIpAddress ?? "127.0.0.1",
                cancellationToken);
        }

        return Result.Success();
    }
}
