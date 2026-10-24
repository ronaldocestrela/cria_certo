using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Features.Login;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Tenancy.Application.Features.ResetPassword;

public record ResetPasswordCommand(
    string Email,
    string Token,
    string NewPassword
) : IRequest<Result>;

public sealed class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, Result>
{
    private readonly ITenancyDbContext _dbContext;

    public ResetPasswordCommandHandler(ITenancyDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var emailLower = request.Email.Trim().ToLowerInvariant();
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == emailLower, cancellationToken);

        if (user == null || user.PasswordResetToken != request.Token.Trim())
        {
            return Result.Failure(
                Error.Validation("Auth.InvalidResetToken", "Token de redefinição inválido ou incorreto."));
        }

        if (user.PasswordResetTokenExpiresAt == null || user.PasswordResetTokenExpiresAt < DateTime.UtcNow)
        {
            return Result.Failure(
                Error.Validation("Auth.ExpiredResetToken", "O token de redefinição expirou. Solicite um novo token."));
        }

        user.PasswordHash = PasswordHasher.Hash(request.NewPassword);
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpiresAt = null;

        if (!user.IsEmailConfirmed)
        {
            user.IsEmailConfirmed = true;
            user.EmailConfirmedAtUtc = DateTime.UtcNow;
            user.EmailConfirmationToken = null;
            user.EmailConfirmationTokenExpiresAt = null;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
