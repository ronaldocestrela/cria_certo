using System.Security.Cryptography;
using CriaCerto.BuildingBlocks.Abstractions.Email;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Features.Login;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Api.Services;

public sealed class PartnerAccountProvisioner : IPartnerAccountProvisioner
{
    private readonly ITenancyDbContext _tenancyDbContext;
    private readonly IEmailSender? _emailSender;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PartnerAccountProvisioner> _logger;

    public PartnerAccountProvisioner(
        ITenancyDbContext tenancyDbContext,
        IConfiguration configuration,
        ILogger<PartnerAccountProvisioner> logger,
        IEmailSender? emailSender = null)
    {
        _tenancyDbContext = tenancyDbContext;
        _configuration = configuration;
        _logger = logger;
        _emailSender = emailSender;
    }

    public async Task<Result<Guid>> ProvisionPartnerUserAsync(
        string fullName,
        string email,
        string? phoneNumber,
        string referralCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();

        var existingUser = await _tenancyDbContext.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (existingUser != null)
        {
            _logger.LogInformation("Parceiro {Email} já possui conta de usuário ativa com ID {UserId}.", normalizedEmail, existingUser.Id);
            return Result.Success(existingUser.Id);
        }

        var confirmationToken = Guid.NewGuid().ToString("N");
        var tempPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));

        var newUser = new User
        {
            Id = Guid.NewGuid(),
            FullName = fullName.Trim(),
            Email = normalizedEmail,
            PasswordHash = PasswordHasher.Hash(tempPassword),
            PhoneNumber = phoneNumber?.Trim(),
            IsEmailConfirmed = false,
            EmailConfirmationToken = confirmationToken,
            EmailConfirmationTokenExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        _tenancyDbContext.Users.Add(newUser);
        await _tenancyDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Conta de usuário provisionada para parceiro {Email} com ID {UserId}.", normalizedEmail, newUser.Id);

        // Disparo de e-mail de ativação e boas-vindas
        if (_emailSender != null)
        {
            var baseUrl = _configuration["App:BaseUrl"] ?? _configuration["Frontend:BaseUrl"] ?? "https://app.criacerto.com.br";
            var activationUrl = $"{baseUrl.TrimEnd('/')}/confirm-email?token={confirmationToken}&email={Uri.EscapeDataString(normalizedEmail)}";
            var referralLink = $"{baseUrl.TrimEnd('/')}/r/{referralCode}";

            var emailBody = $@"
                <div style=""font-family: sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; color: #1e293b;"">
                    <h2 style=""color: #15803d;"">Bem-vindo ao Programa de Parceiros do CriaCerto!</h2>
                    <p>Olá, <strong>{fullName}</strong>,</p>
                    <p>Você foi cadastrado como Parceiro Comercial oficial da plataforma <strong>CriaCerto</strong>.</p>
                    <div style=""background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px; margin: 20px 0;"">
                        <p style=""margin: 0 0 8px 0;""><strong>Seu Link Oficial de Indicação:</strong></p>
                        <a href=""{referralLink}"" style=""color: #2563eb; font-weight: bold; text-decoration: none;"">{referralLink}</a>
                    </div>
                    <p>Para ativar sua conta e acessar seu painel comercial exclusivo, clique no botão abaixo:</p>
                    <div style=""margin: 25px 0;"">
                        <a href=""{activationUrl}"" style=""background-color: #15803d; color: #ffffff; padding: 12px 24px; text-decoration: none; border-radius: 6px; font-weight: bold; display: inline-block;"">
                            Ativar Minha Conta de Parceiro &rarr;
                        </a>
                    </div>
                    <p style=""font-size: 13px; color: #64748b;"">Este link de ativação é válido por 7 dias. Caso não tenha solicitado este cadastro, favor ignorar.</p>
                </div>";

            try
            {
                var emailMessage = EmailMessage.Create(
                    normalizedEmail,
                    "Bem-vindo ao Programa de Parceiros CriaCerto - Ativação de Acesso",
                    emailBody,
                    toName: fullName);

                await _emailSender.SendAsync(emailMessage, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao enviar e-mail de ativação para parceiro {Email}.", normalizedEmail);
            }
        }

        return Result.Success(newUser.Id);
    }
}
