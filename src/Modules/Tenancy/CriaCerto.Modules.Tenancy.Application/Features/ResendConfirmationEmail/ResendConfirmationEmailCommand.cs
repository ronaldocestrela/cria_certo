using CriaCerto.BuildingBlocks.Abstractions.Email;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Tenancy.Application.Features.ResendConfirmationEmail;

public record ResendConfirmationEmailCommand(string Email) : IRequest<Result<string>>;

public sealed class ResendConfirmationEmailCommandValidator : AbstractValidator<ResendConfirmationEmailCommand>
{
    public ResendConfirmationEmailCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("O e-mail é obrigatório.")
            .EmailAddress().WithMessage("Formato de e-mail inválido.");
    }
}

public sealed class ResendConfirmationEmailCommandHandler : IRequestHandler<ResendConfirmationEmailCommand, Result<string>>
{
    private readonly ITenancyDbContext _dbContext;
    private readonly IEmailSender? _emailSender;

    public ResendConfirmationEmailCommandHandler(ITenancyDbContext dbContext, IEmailSender? emailSender = null)
    {
        _dbContext = dbContext;
        _emailSender = emailSender;
    }

    public async Task<Result<string>> Handle(ResendConfirmationEmailCommand request, CancellationToken cancellationToken)
    {
        var emailLower = request.Email.Trim().ToLowerInvariant();
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == emailLower, cancellationToken);

        if (user is null)
        {
            // Retorna sucesso com string vazia para evitar enumeração de contas
            return Result.Success(string.Empty);
        }

        if (user.IsEmailConfirmed)
        {
            return Result.Failure<string>(
                Error.Conflict("Auth.EmailAlreadyConfirmed", "Este e-mail já foi confirmado anteriormente. Faça login na sua conta."));
        }

        var token = Guid.NewGuid().ToString("N");
        user.EmailConfirmationToken = token;
        user.EmailConfirmationTokenExpiresAt = DateTime.UtcNow.AddHours(24);

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (_emailSender is not null)
        {
            var html = $"""
                <div style="font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; color: #1f2937; background-color: #ffffff; border-radius: 8px; border: 1px solid #e5e7eb;">
                    <div style="text-align: center; margin-bottom: 24px;">
                        <h1 style="color: #15803d; font-size: 26px; margin: 0; font-weight: 700;">Confirmação de E-mail - CriaCerto</h1>
                        <p style="color: #6b7280; font-size: 14px; margin-top: 4px;">Gestão Pecuária Inteligente e de Alta Performance</p>
                    </div>
                    <p style="font-size: 16px; line-height: 1.5;">Olá, <strong>{user.FullName}</strong>,</p>
                    <p style="font-size: 15px; line-height: 1.6; color: #374151;">
                        Você solicitou um novo link para confirmar seu endereço de e-mail e ativar o acesso à sua fazenda na plataforma CriaCerto.
                    </p>
                    <div style="background-color: #f0fdf4; border: 1px solid #bbf7d0; border-radius: 8px; padding: 20px; margin: 24px 0; text-align: center;">
                        <p style="font-size: 14px; color: #166534; margin-bottom: 16px; font-weight: 600;">Clique no botão abaixo para confirmar seu e-mail:</p>
                        <a href="/confirm-email?token={token}&email={Uri.EscapeDataString(user.Email)}" 
                           style="display: inline-block; background-color: #15803d; color: #ffffff; font-weight: 700; font-size: 15px; padding: 12px 28px; text-decoration: none; border-radius: 6px;">
                            Confirmar Meu E-mail
                        </a>
                        <p style="font-size: 12px; color: #6b7280; margin-top: 16px; margin-bottom: 0;">
                            Código de confirmação: <code style="font-family: monospace; background: #ffffff; padding: 2px 6px; border-radius: 4px; border: 1px solid #d1d5db;">{token}</code>
                        </p>
                    </div>
                    <p style="font-size: 13px; color: #6b7280; text-align: center;">
                        Este link é válido por <strong>24 horas</strong>. Se você não solicitou esta confirmação, desconsidere esta mensagem.
                    </p>
                    <hr style="border: none; border-top: 1px solid #e5e7eb; margin: 24px 0;" />
                    <p style="font-size: 12px; color: #9ca3af; text-align: center; margin: 0;">
                        CriaCerto Agro &copy; {DateTime.UtcNow.Year} - Todos os direitos reservados.
                    </p>
                </div>
                """;

            var emailMessage = EmailMessage.Create(
                toEmail: user.Email,
                subject: "Confirme seu e-mail no CriaCerto",
                htmlBody: html,
                textBody: $"Olá {user.FullName}! Confirme seu e-mail no CriaCerto acessando /confirm-email?token={token}&email={user.Email} (válido por 24h). Código: {token}",
                toName: user.FullName);

            await _emailSender.SendAsync(emailMessage, cancellationToken);
        }

        return Result.Success(token);
    }
}
