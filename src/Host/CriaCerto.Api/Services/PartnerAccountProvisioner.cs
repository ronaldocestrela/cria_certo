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
        var baseUrl = (_configuration["App:WebBaseUrl"] 
            ?? _configuration["WEB_BASE_URL"] 
            ?? _configuration["App:BaseUrl"] 
            ?? _configuration["Frontend:BaseUrl"] 
            ?? "http://localhost:8081").TrimEnd('/');

        var referralLink = $"{baseUrl}/r/{referralCode}";
        var loginUrl = $"{baseUrl}/login";

        var existingUser = await _tenancyDbContext.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (existingUser != null)
        {
            _logger.LogInformation("Parceiro {Email} já possui conta de usuário ativa com ID {UserId}. Vinculando e notificando.", normalizedEmail, existingUser.Id);

            // Disparo de e-mail de boas-vindas e orientações para usuário que já possui conta
            if (_emailSender != null)
            {
                var existingUserEmailBody = $@"
                    <div style=""font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; color: #1e293b; background: #ffffff; border-radius: 12px; border: 1px solid #e2e8f0;"">
                        <div style=""text-align: center; margin-bottom: 24px;"">
                            <h2 style=""color: #15803d; margin: 0 0 8px 0; font-size: 22px;"">Você agora é um Parceiro Oficial CriaCerto!</h2>
                            <p style=""color: #64748b; font-size: 14px; margin: 0;"">Seu perfil comercial de indicação foi ativado com sucesso.</p>
                        </div>
                        <p style=""font-size: 15px; line-height: 1.5;"">Olá, <strong>{fullName}</strong>,</p>
                        <p style=""font-size: 15px; line-height: 1.5;"">Sua conta no CriaCerto foi habilitada no <strong>Programa de Parceiros Comerciais</strong>. A partir de agora, você já pode começar a divulgar e monetizar suas indicações.</p>
                        
                        <div style=""background: #f0fdf4; border: 1px solid #bbf7d0; border-radius: 8px; padding: 16px; margin: 24px 0;"">
                            <p style=""margin: 0 0 6px 0; font-size: 13px; font-weight: bold; color: #166534; text-transform: uppercase; letter-spacing: 0.5px;"">Seu Link Oficial de Indicação:</p>
                            <a href=""{referralLink}"" style=""color: #15803d; font-weight: bold; font-size: 16px; text-decoration: underline; word-break: break-all;"">{referralLink}</a>
                            <p style=""margin: 8px 0 0 0; font-size: 12px; color: #166534;"">Compartilhe este link com produtores, veterinários e fazendas para rastrear suas comissões.</p>
                        </div>

                        <div style=""background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px; margin: 20px 0;"">
                            <p style=""margin: 0; font-size: 14px; color: #334155;"">
                                <strong>Como acessar:</strong> Como você já possui uma conta ativa no CriaCerto, basta fazer login com seu e-mail e suas credenciais habituais. Seu painel de parceiro já estará acessível.
                            </p>
                        </div>

                        <div style=""text-align: center; margin: 28px 0;"">
                            <a href=""{loginUrl}"" style=""background-color: #15803d; color: #ffffff; padding: 14px 28px; text-decoration: none; border-radius: 8px; font-weight: bold; font-size: 15px; display: inline-block;"">
                                Acessar Painel do Parceiro &rarr;
                            </a>
                        </div>
                        
                        <hr style=""border: none; border-top: 1px solid #e2e8f0; margin: 24px 0;"" />
                        <p style=""font-size: 12px; color: #94a3b8; text-align: center; margin: 0;"">CriaCerto Gestão Pecuária Inteligente &bull; Programa de Parcerias</p>
                    </div>";

                try
                {
                    var emailMessage = EmailMessage.Create(
                        normalizedEmail,
                        "Você agora é um Parceiro Comercial CriaCerto - Seu Link Oficial de Indicação",
                        existingUserEmailBody,
                        textBody: $"Olá {fullName}, você foi habilitado como Parceiro Comercial no CriaCerto. Seu link oficial de indicação é {referralLink}. Acesse o portal em {loginUrl} com suas credenciais habituais.",
                        toName: fullName);

                    await _emailSender.SendAsync(emailMessage, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Falha ao enviar e-mail de notificação de parceria para {Email}.", normalizedEmail);
                }
            }

            return Result.Success(existingUser.Id);
        }

        var confirmationToken = Guid.NewGuid().ToString("N");
        var resetToken = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
        var tempPassword = $"CriaCerto#{RandomNumberGenerator.GetInt32(100000, 999999)}";

        var newUser = new User
        {
            Id = Guid.NewGuid(),
            FullName = fullName.Trim(),
            Email = normalizedEmail,
            PasswordHash = PasswordHasher.Hash(tempPassword),
            PhoneNumber = phoneNumber?.Trim(),
            IsEmailConfirmed = false,
            EmailConfirmationToken = confirmationToken,
            EmailConfirmationTokenExpiresAt = DateTime.UtcNow.AddDays(7),
            PasswordResetToken = resetToken,
            PasswordResetTokenExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        _tenancyDbContext.Users.Add(newUser);
        await _tenancyDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Conta de usuário provisionada para parceiro {Email} com ID {UserId}.", normalizedEmail, newUser.Id);

        // Disparo de e-mail de ativação e boas-vindas com credenciais de primeiro acesso
        if (_emailSender != null)
        {
            var activationUrl = $"{baseUrl}/confirm-email?token={confirmationToken}&email={Uri.EscapeDataString(normalizedEmail)}";
            var setPasswordUrl = $"{baseUrl}/forgot-password?email={Uri.EscapeDataString(normalizedEmail)}&token={resetToken}";

            var emailBody = $@"
                <div style=""font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; color: #1e293b; background: #ffffff; border-radius: 12px; border: 1px solid #e2e8f0;"">
                    <div style=""text-align: center; margin-bottom: 24px;"">
                        <h2 style=""color: #15803d; margin: 0 0 8px 0; font-size: 22px;"">Bem-vindo ao Programa de Parceiros CriaCerto!</h2>
                        <p style=""color: #64748b; font-size: 14px; margin: 0;"">Sua conta comercial de parceiro foi gerada com sucesso.</p>
                    </div>

                    <p style=""font-size: 15px; line-height: 1.5;"">Olá, <strong>{fullName}</strong>,</p>
                    <p style=""font-size: 15px; line-height: 1.5;"">Você foi cadastrado como Parceiro Comercial oficial da plataforma <strong>CriaCerto</strong>. Abaixo estão suas credenciais de primeiro acesso e seu link de indicação:</p>

                    <div style=""background: #f0fdf4; border: 1px solid #bbf7d0; border-radius: 8px; padding: 16px; margin: 20px 0;"">
                        <p style=""margin: 0 0 6px 0; font-size: 13px; font-weight: bold; color: #166534; text-transform: uppercase; letter-spacing: 0.5px;"">Seu Link Oficial de Indicação:</p>
                        <a href=""{referralLink}"" style=""color: #15803d; font-weight: bold; font-size: 16px; text-decoration: underline; word-break: break-all;"">{referralLink}</a>
                    </div>

                    <div style=""background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px; margin: 20px 0;"">
                        <p style=""margin: 0 0 10px 0; font-size: 13px; font-weight: bold; color: #334155; text-transform: uppercase; letter-spacing: 0.5px;"">Credenciais de Acesso Provisórias:</p>
                        <p style=""margin: 0 0 6px 0; font-size: 14px;""><strong>E-mail:</strong> {normalizedEmail}</p>
                        <p style=""margin: 0 0 8px 0; font-size: 14px;""><strong>Senha Temporária:</strong> <code style=""background: #e2e8f0; padding: 2px 6px; border-radius: 4px; font-family: monospace; font-size: 15px; font-weight: bold; color: #0f172a;"">{tempPassword}</code></p>
                        <p style=""margin: 8px 0 0 0; font-size: 12px; color: #64748b;"">Guarde esta senha temporária para seu primeiro acesso ao sistema.</p>
                    </div>

                    <p style=""font-size: 14px; margin-top: 20px;"">Para confirmar sua conta e acessar seu painel comercial exclusivo, clique no botão abaixo:</p>

                    <div style=""text-align: center; margin: 25px 0;"">
                        <a href=""{activationUrl}"" style=""background-color: #15803d; color: #ffffff; padding: 14px 28px; text-decoration: none; border-radius: 8px; font-weight: bold; font-size: 15px; display: inline-block;"">
                            Ativar Minha Conta de Parceiro &rarr;
                        </a>
                    </div>

                    <div style=""text-align: center; margin: 15px 0;"">
                        <p style=""font-size: 13px; color: #64748b; margin: 0 0 6px 0;"">Prefere escolher sua própria senha agora mesmo?</p>
                        <a href=""{setPasswordUrl}"" style=""color: #2563eb; font-size: 14px; font-weight: 600; text-decoration: underline;"">
                            Clique aqui para definir sua senha pessoal
                        </a>
                    </div>

                    <hr style=""border: none; border-top: 1px solid #e2e8f0; margin: 24px 0;"" />
                    <p style=""font-size: 12px; color: #94a3b8; text-align: center; margin: 0;"">Este link de ativação é válido por 7 dias. CriaCerto Gestão Pecuária Inteligente.</p>
                </div>";

            try
            {
                var emailMessage = EmailMessage.Create(
                    normalizedEmail,
                    "Bem-vindo ao Programa de Parceiros CriaCerto - Ativação de Acesso",
                    emailBody,
                    textBody: $"Olá {fullName}, você foi cadastrado como Parceiro Comercial no CriaCerto. Link de Indicação: {referralLink}. E-mail: {normalizedEmail}. Senha Temporária: {tempPassword}. Link de Ativação: {activationUrl}. Ou defina sua senha em: {setPasswordUrl}.",
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
