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

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            PasswordHash = PasswordHasher.Hash(request.Password),
            PhoneNumber = request.PhoneNumber?.Trim()
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
                        Sua conta foi criada com sucesso! Estamos muito felizes em ter você conosco na transformação digital e zootécnica da sua pecuária.
                    </p>
                    <div style="background-color: #f0fdf4; border: 1px solid #bbf7d0; border-radius: 8px; padding: 16px; margin: 20px 0;">
                        <h3 style="color: #166534; margin: 0 0 8px 0; font-size: 16px;">Próximos passos recomendados:</h3>
                        <ul style="margin: 0; padding-left: 20px; color: #15803d; font-size: 14px; line-height: 1.6;">
                            <li>Cadastre os dados da sua fazenda no assistente inicial.</li>
                            <li>Defina seus lotes, pastos e piquetes.</li>
                            <li>Comece a registrar pesagens, manejos sanitários e reprodutivos.</li>
                        </ul>
                    </div>
                    <p style="font-size: 14px; color: #6b7280; margin-top: 24px; text-align: center;">
                        Se tiver qualquer dúvida, responda diretamente a este e-mail ou entre em contato com nosso time de suporte.
                    </p>
                    <hr style="border: none; border-top: 1px solid #e5e7eb; margin: 24px 0;" />
                    <p style="font-size: 12px; color: #9ca3af; text-align: center; margin: 0;">
                        CriaCerto Agro &copy; {DateTime.UtcNow.Year} - Todos os direitos reservados.
                    </p>
                </div>
                """;

            var emailMessage = CriaCerto.BuildingBlocks.Abstractions.Email.EmailMessage.Create(
                toEmail: user.Email,
                subject: "Bem-vindo ao CriaCerto!",
                htmlBody: html,
                textBody: $"Olá, {user.FullName}! Sua conta foi criada com sucesso no CriaCerto. Acesse a plataforma para cadastrar sua fazenda e começar a gerenciar seu rebanho.",
                toName: user.FullName);

            await _emailSender.SendAsync(emailMessage, cancellationToken);
        }

        return Result.Success(new UserDto(user.Id, user.FullName, user.Email, user.PhoneNumber));
    }
}
