using CriaCerto.BuildingBlocks.Abstractions.Email;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Tenancy.Application.Features.ForgotPassword;

public record ForgotPasswordCommand(string Email) : IRequest<Result<string>>;

public sealed class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand, Result<string>>
{
    private readonly ITenancyDbContext _dbContext;
    private readonly IEmailSender? _emailSender;

    public ForgotPasswordCommandHandler(ITenancyDbContext dbContext, IEmailSender? emailSender = null)
    {
        _dbContext = dbContext;
        _emailSender = emailSender;
    }

    public async Task<Result<string>> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var emailLower = request.Email.Trim().ToLowerInvariant();
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == emailLower, cancellationToken);

        if (user == null)
        {
            return Result.Success(string.Empty);
        }

        var token = Random.Shared.Next(100000, 999999).ToString();
        user.PasswordResetToken = token;
        user.PasswordResetTokenExpiresAt = DateTime.UtcNow.AddHours(1);

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (_emailSender != null)
        {
            var html = $"""
                <div style="font-family: sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; color: #333;">
                    <h2 style="color: #2e7d32;">Recuperação de Senha - CriaCerto</h2>
                    <p>Olá,</p>
                    <p>Recebemos uma solicitação para redefinir a sua senha de acesso à plataforma CriaCerto.</p>
                    <p>Seu código de verificação é:</p>
                    <div style="background-color: #f1f8e9; border: 1px solid #c8e6c9; border-radius: 8px; padding: 16px; font-size: 24px; font-weight: bold; text-align: center; letter-spacing: 4px; color: #1b5e20;">
                        {token}
                    </div>
                    <p style="margin-top: 20px; font-size: 14px; color: #666;">Este código expira em 1 hora. Se você não solicitou a redefinição de senha, desconsidere esta mensagem.</p>
                </div>
                """;

            var emailMessage = EmailMessage.Create(
                toEmail: user.Email,
                subject: "CriaCerto - Recuperação de Senha",
                htmlBody: html,
                textBody: $"Seu código de verificação para recuperação de senha no CriaCerto é: {token}. Ele expira em 1 hora.",
                toName: user.FullName);

            await _emailSender.SendAsync(emailMessage, cancellationToken);
        }

        return Result.Success(token);
    }
}
