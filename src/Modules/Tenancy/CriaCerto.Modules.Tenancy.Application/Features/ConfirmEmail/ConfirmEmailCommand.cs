using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Tenancy.Application.Features.ConfirmEmail;

public record ConfirmEmailCommand(string Email, string Token) : IRequest<Result>;

public sealed class ConfirmEmailCommandValidator : AbstractValidator<ConfirmEmailCommand>
{
    public ConfirmEmailCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("O e-mail é obrigatório.")
            .EmailAddress().WithMessage("Formato de e-mail inválido.");

        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("O token de confirmação é obrigatório.");
    }
}

public sealed class ConfirmEmailCommandHandler : IRequestHandler<ConfirmEmailCommand, Result>
{
    private readonly ITenancyDbContext _dbContext;

    public ConfirmEmailCommandHandler(ITenancyDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result> Handle(ConfirmEmailCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (user is null)
        {
            return Result.Failure(
                Error.NotFound("User.NotFound", "Usuário não encontrado."));
        }

        if (user.IsEmailConfirmed)
        {
            return Result.Success();
        }

        if (string.IsNullOrWhiteSpace(user.EmailConfirmationToken) ||
            !string.Equals(user.EmailConfirmationToken.Trim(), request.Token.Trim(), StringComparison.Ordinal))
        {
            return Result.Failure(
                Error.Validation("Auth.InvalidConfirmationToken", "Token de confirmação inválido ou incorreto."));
        }

        if (user.EmailConfirmationTokenExpiresAt.HasValue && user.EmailConfirmationTokenExpiresAt.Value < DateTime.UtcNow)
        {
            return Result.Failure(
                Error.Validation("Auth.ConfirmationTokenExpired", "O link de confirmação expirou. Solicite um novo link de ativação."));
        }

        user.IsEmailConfirmed = true;
        user.EmailConfirmedAtUtc = DateTime.UtcNow;
        user.EmailConfirmationToken = null;
        user.EmailConfirmationTokenExpiresAt = null;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
