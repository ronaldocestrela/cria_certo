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
    private readonly Microsoft.Extensions.Configuration.IConfiguration? _configuration;

    public ResendConfirmationEmailCommandHandler(
        ITenancyDbContext dbContext,
        IEmailSender? emailSender = null,
        Microsoft.Extensions.Configuration.IConfiguration? configuration = null)
    {
        _dbContext = dbContext;
        _emailSender = emailSender;
        _configuration = configuration;
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
            var webBaseUrl = (_configuration?["App:WebBaseUrl"] 
                ?? _configuration?["WEB_BASE_URL"] 
                ?? "http://localhost:8081").TrimEnd('/');
            var confirmationLink = $"{webBaseUrl}/confirm-email?token={token}&email={Uri.EscapeDataString(user.Email)}";

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
                    <div style="background-color: #f0fdf4; border: 1px solid #bbf7d0; border-radius: 8px; padding: 24px; margin: 24px 0; text-align: center;">
                        <table border="0" cellpadding="0" cellspacing="0" role="presentation" style="margin: 0 auto;">
                            <tr>
                                <td align="center" style="border-radius: 8px; background-color: #15803d;">
                                    <a href="{confirmationLink}" target="_blank" 
                                       style="display: inline-block; background-color: #15803d; color: #ffffff; font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; font-size: 16px; font-weight: 700; text-decoration: none; padding: 14px 32px; border-radius: 8px; border: 1px solid #15803d; cursor: pointer;">
                                        Confirmar Meu E-mail e Ativar Conta &rarr;
                                    </a>
                                </td>
                            </tr>
                        </table>
                        <p style="font-size: 13px; color: #4b5563; margin-top: 18px; margin-bottom: 0;">
                            Se o botão acima não funcionar, clique no link abaixo ou copie-o no seu navegador:<br/>
                            <a href="{confirmationLink}" target="_blank" style="color: #15803d; word-break: break-all; font-size: 12px; font-weight: 600; text-decoration: underline;">{confirmationLink}</a>
                        </p>
                        <p style="font-size: 12px; color: #6b7280; margin-top: 12px; margin-bottom: 0;">
                            Código de confirmação manual: <code style="font-family: monospace; background: #ffffff; padding: 2px 6px; border-radius: 4px; border: 1px solid #d1d5db; color: #111827;">{token}</code>
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
                textBody: $"Olá {user.FullName}! Confirme seu e-mail no CriaCerto clicando em {confirmationLink} (válido por 24h, código: {token}).",
                toName: user.FullName);

            await _emailSender.SendAsync(emailMessage, cancellationToken);
        }

        return Result.Success(token);
    }
}
