using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Contracts;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Features.Login;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Tenancy.Application.Features.RegisterUser;

public record RegisterUserCommand(
    string FullName,
    string Email,
    string Password,
    string? PhoneNumber = null
) : IRequest<Result<UserDto>>;

public sealed class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, Result<UserDto>>
{
    private readonly ITenancyDbContext _dbContext;
    private readonly CriaCerto.BuildingBlocks.Abstractions.Email.IEmailSender? _emailSender;

    public RegisterUserCommandHandler(
        ITenancyDbContext dbContext,
        CriaCerto.BuildingBlocks.Abstractions.Email.IEmailSender? emailSender = null)
    {
        _dbContext = dbContext;
        _emailSender = emailSender;
    }

    public async Task<Result<UserDto>> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        var existingUser = await _dbContext.Users
            .AnyAsync(u => u.Email == request.Email, cancellationToken);

        if (existingUser)
        {
            return Result.Failure<UserDto>(
                Error.Conflict("User.EmailAlreadyExists", "Este e-mail já está em uso por outra conta."));
        }

        var confirmationToken = Guid.NewGuid().ToString("N");
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            PasswordHash = PasswordHasher.Hash(request.Password),
            PhoneNumber = request.PhoneNumber?.Trim(),
            IsEmailConfirmed = false,
            EmailConfirmationToken = confirmationToken,
            EmailConfirmationTokenExpiresAt = DateTime.UtcNow.AddHours(24)
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (_emailSender != null)
        {
            var html = $"""
                <div style="font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; color: #1f2937; background-color: #ffffff; border-radius: 8px; border: 1px solid #e5e7eb;">
                    <div style="text-align: center; margin-bottom: 24px;">
                        <h1 style="color: #15803d; font-size: 26px; margin: 0; font-weight: 700;">Bem-vindo ao CriaCerto!</h1>
                        <p style="color: #6b7280; font-size: 14px; margin-top: 4px;">Gestão Pecuária Inteligente e de Alta Performance</p>
                    </div>
                    <p style="font-size: 16px; line-height: 1.5;">Olá, <strong>{user.FullName}</strong>,</p>
                    <p style="font-size: 15px; line-height: 1.6; color: #374151;">
                        Sua conta foi criada com sucesso! Para começar a utilizar a plataforma e gerenciar sua fazenda, confirme o seu endereço de e-mail clicando no botão abaixo:
                    </p>
                    <div style="background-color: #f0fdf4; border: 1px solid #bbf7d0; border-radius: 8px; padding: 20px; margin: 24px 0; text-align: center;">
                        <a href="/confirm-email?token={confirmationToken}&email={Uri.EscapeDataString(user.Email)}" 
                           style="display: inline-block; background-color: #15803d; color: #ffffff; font-weight: 700; font-size: 15px; padding: 12px 28px; text-decoration: none; border-radius: 6px;">
                            Confirmar Meu E-mail
                        </a>
                        <p style="font-size: 12px; color: #6b7280; margin-top: 14px; margin-bottom: 0;">
                            Código de confirmação: <code style="font-family: monospace; background: #ffffff; padding: 2px 6px; border-radius: 4px; border: 1px solid #d1d5db;">{confirmationToken}</code>
                        </p>
                    </div>
                    <div style="background-color: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px; margin: 20px 0;">
                        <h3 style="color: #1e293b; margin: 0 0 8px 0; font-size: 15px;">Próximos passos recomendados:</h3>
                        <ul style="margin: 0; padding-left: 20px; color: #475569; font-size: 14px; line-height: 1.6;">
                            <li>Confirme seu e-mail e ative sua conta.</li>
                            <li>Cadastre os dados da sua fazenda no assistente inicial.</li>
                            <li>Defina seus lotes, pastos e piquetes para gestão de UA/ha.</li>
                            <li>Comece a registrar pesagens, manejos sanitários e reprodutivos.</li>
                        </ul>
                    </div>
                    <p style="font-size: 13px; color: #6b7280; margin-top: 24px; text-align: center;">
                        Este link de confirmação expira em 24 horas. Se tiver qualquer dúvida, responda diretamente a este e-mail.
                    </p>
                    <hr style="border: none; border-top: 1px solid #e5e7eb; margin: 24px 0;" />
                    <p style="font-size: 12px; color: #9ca3af; text-align: center; margin: 0;">
                        CriaCerto Agro &copy; {DateTime.UtcNow.Year} - Todos os direitos reservados.
                    </p>
                </div>
                """;

            var emailMessage = CriaCerto.BuildingBlocks.Abstractions.Email.EmailMessage.Create(
                toEmail: user.Email,
                subject: "Bem-vindo ao CriaCerto! Confirme seu e-mail",
                htmlBody: html,
                textBody: $"Olá, {user.FullName}! Sua conta foi criada no CriaCerto. Confirme seu e-mail em /confirm-email?token={confirmationToken}&email={user.Email} (código: {confirmationToken}) para começar.",
                toName: user.FullName);

            await _emailSender.SendAsync(emailMessage, cancellationToken);
        }

        return Result.Success(new UserDto(user.Id, user.FullName, user.Email, user.PhoneNumber));
    }
}
