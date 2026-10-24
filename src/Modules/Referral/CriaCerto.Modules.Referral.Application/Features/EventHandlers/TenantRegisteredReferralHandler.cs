using System.Text.Json;
using System.Text.RegularExpressions;
using CriaCerto.Modules.Referral.Application.Abstractions;
using CriaCerto.Modules.Referral.Application.Domain;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Tenancy.Application.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CriaCerto.Modules.Referral.Application.Features.EventHandlers;

public sealed class TenantRegisteredReferralHandler : INotificationHandler<TenantRegisteredIntegrationEvent>
{
    private readonly IReferralDbContext _dbContext;
    private readonly ITenantInfoLookupService? _tenantLookupService;
    private readonly IReferralAuditSink? _auditSink;
    private readonly ILogger<TenantRegisteredReferralHandler>? _logger;

    public TenantRegisteredReferralHandler(
        IReferralDbContext dbContext,
        ITenantInfoLookupService? tenantLookupService = null,
        IReferralAuditSink? auditSink = null,
        ILogger<TenantRegisteredReferralHandler>? logger = null)
    {
        _dbContext = dbContext;
        _tenantLookupService = tenantLookupService;
        _auditSink = auditSink;
        _logger = logger;
    }

    public async Task Handle(TenantRegisteredIntegrationEvent notification, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(notification.ReferralCode))
        {
            return;
        }

        var normalizedCode = notification.ReferralCode.Trim().ToUpperInvariant();

        var referralCode = await _dbContext.ReferralCodes
            .FirstOrDefaultAsync(c => c.Code == normalizedCode, cancellationToken);

        if (referralCode == null)
        {
            _logger?.LogWarning("Código de indicação '{ReferralCode}' não encontrado ao provisionar tenant {TenantId}.",
                notification.ReferralCode, notification.TenantId);
            return;
        }

        if (!referralCode.IsActive)
        {
            _logger?.LogWarning("Código de indicação '{ReferralCode}' está inativo ao provisionar tenant {TenantId}.",
                notification.ReferralCode, notification.TenantId);
            return;
        }

        var participant = await _dbContext.Participants
            .FirstOrDefaultAsync(p => p.Id == referralCode.ParticipantId, cancellationToken);

        if (participant == null)
        {
            _logger?.LogWarning("Participante {ParticipantId} não encontrado para o código '{ReferralCode}'.",
                referralCode.ParticipantId, notification.ReferralCode);
            return;
        }

        // Idempotência: impede duplicidade de atribuição para o mesmo tenant
        var alreadyAttributed = await _dbContext.Attributions
            .AnyAsync(a => a.ReferredTenantId == notification.TenantId, cancellationToken);

        if (alreadyAttributed)
        {
            _logger?.LogInformation("Tenant {TenantId} já possui atribuição de indicação registrada.", notification.TenantId);
            return;
        }

        // Validação anti-fraude: detecção de auto-indicação
        var fraudReason = await DetectSelfReferralFraudAsync(participant, notification, cancellationToken);
        if (fraudReason != null)
        {
            _logger?.LogWarning("Auto-indicação fraudulenta detectada para Tenant {TenantId} e código '{ReferralCode}': {Reason}",
                notification.TenantId, notification.ReferralCode, fraudReason);

            var fraudAttribution = ReferralAttribution.Create(
                referralCodeId: referralCode.Id,
                referrerParticipantId: participant.Id,
                referredTenantId: notification.TenantId,
                originIpAddress: notification.OriginIpAddress,
                userAgent: notification.UserAgent);

            if (fraudAttribution.IsSuccess)
            {
                fraudAttribution.Value.FlagAsFraud(fraudReason);
                _dbContext.Attributions.Add(fraudAttribution.Value);
                await _dbContext.SaveChangesAsync(cancellationToken);

                if (_auditSink != null)
                {
                    var auditDetails = JsonSerializer.Serialize(new
                    {
                        Action = "SelfReferralBlocked",
                        ReferralCode = notification.ReferralCode,
                        ReferrerParticipantId = participant.Id,
                        ReferredTenantId = notification.TenantId,
                        FraudReason = fraudReason
                    });

                    await _auditSink.RecordSystemAuditAsync(
                        action: "Referral.AntiFraud.SelfReferralBlocked",
                        resource: $"ReferralAttribution/{fraudAttribution.Value.Id}",
                        targetTenantId: notification.TenantId,
                        detailsJson: auditDetails,
                        cancellationToken: cancellationToken);
                }

                Referral.Application.Telemetry.ReferralTelemetry.RecordAttribution(participant.ParticipantType.ToString(), "Fraudulent");
            }
            return;
        }

        var attributionResult = ReferralAttribution.Create(
            referralCodeId: referralCode.Id,
            referrerParticipantId: participant.Id,
            referredTenantId: notification.TenantId,
            originIpAddress: notification.OriginIpAddress,
            userAgent: notification.UserAgent);

        if (attributionResult.IsSuccess)
        {
            _dbContext.Attributions.Add(attributionResult.Value);
            Referral.Application.Telemetry.ReferralTelemetry.RecordAttribution(participant.ParticipantType.ToString(), "Pending");
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger?.LogInformation("Atribuição de indicação criada com sucesso para Tenant {TenantId} (Status: Pending, Código: '{ReferralCode}').",
                notification.TenantId, notification.ReferralCode);
        }
        else
        {
            _logger?.LogError("Falha ao criar atribuição de indicação para Tenant {TenantId}: {Error}",
                notification.TenantId, attributionResult.Error.Message);
        }
    }

    private async Task<string?> DetectSelfReferralFraudAsync(
        ReferralProgramParticipant participant,
        TenantRegisteredIntegrationEvent notification,
        CancellationToken cancellationToken)
    {
        // 1. Produtor indicando o mesmo ID de tenant
        if (participant.ParticipantType == ParticipantType.Customer &&
            participant.TenantId.HasValue &&
            participant.TenantId.Value == notification.TenantId)
        {
            return "Auto-indicação detectada: o participante indicador pertence à mesma fazenda indicada.";
        }

        if (_tenantLookupService == null)
        {
            return null;
        }

        var referredTenant = await _tenantLookupService.GetTenantSummaryAsync(notification.TenantId, cancellationToken);
        if (referredTenant == null)
        {
            return null;
        }

        var cleanReferredCnpj = CleanDigits(referredTenant.CnpjNormalized);
        var cleanNotificationEmail = notification.UserEmail?.Trim().ToLowerInvariant();

        // 2. Produtor indicando outra conta com mesmo CPF/CNPJ ou mesmo titular
        if (participant.ParticipantType == ParticipantType.Customer && participant.TenantId.HasValue)
        {
            var referrerTenant = await _tenantLookupService.GetTenantSummaryAsync(participant.TenantId.Value, cancellationToken);
            if (referrerTenant != null)
            {
                var cleanReferrerCnpj = CleanDigits(referrerTenant.CnpjNormalized);
                if (!string.IsNullOrEmpty(cleanReferrerCnpj) &&
                    !string.IsNullOrEmpty(cleanReferredCnpj) &&
                    string.Equals(cleanReferrerCnpj, cleanReferredCnpj, StringComparison.OrdinalIgnoreCase))
                {
                    return "Auto-indicação detectada: mesmo CPF/CNPJ cadastrado entre o participante indicador e o novo tenant.";
                }

                if (IsMatchingEmail(referrerTenant.TechnicalOwnerEmail, referredTenant.TechnicalOwnerEmail) ||
                    IsMatchingEmail(referrerTenant.TechnicalOwnerEmail, referredTenant.CommercialOwnerEmail) ||
                    IsMatchingEmail(referrerTenant.TechnicalOwnerEmail, cleanNotificationEmail) ||
                    IsMatchingEmail(referrerTenant.CommercialOwnerEmail, referredTenant.TechnicalOwnerEmail) ||
                    IsMatchingEmail(referrerTenant.CommercialOwnerEmail, referredTenant.CommercialOwnerEmail) ||
                    IsMatchingEmail(referrerTenant.CommercialOwnerEmail, cleanNotificationEmail))
                {
                    return "Auto-indicação detectada: mesmo titular/e-mail cadastrado entre o participante indicador e o novo tenant.";
                }
            }
        }

        // 3. Parceiro comercial indicando conta própria (mesmo CPF/CNPJ ou e-mail cadastrado no contrato/perfil)
        if (participant.ParticipantType == ParticipantType.CommercialPartner)
        {
            var contract = await _dbContext.PartnerContracts
                .FirstOrDefaultAsync(c => c.ParticipantId == participant.Id && c.IsActive, cancellationToken);

            if (contract != null)
            {
                var cleanContractDoc = CleanDigits(contract.LegalDocumentNumber);
                if (!string.IsNullOrEmpty(cleanContractDoc) &&
                    !string.IsNullOrEmpty(cleanReferredCnpj) &&
                    string.Equals(cleanContractDoc, cleanReferredCnpj, StringComparison.OrdinalIgnoreCase))
                {
                    return "Auto-indicação detectada: o CPF/CNPJ do contrato do parceiro coincide com o documento do tenant indicado.";
                }
            }

            var profile = await _dbContext.PartnerProfiles
                .FirstOrDefaultAsync(p => p.ParticipantId == participant.Id, cancellationToken);

            if (profile != null)
            {
                if (IsMatchingEmail(profile.Email, referredTenant.TechnicalOwnerEmail) ||
                    IsMatchingEmail(profile.Email, referredTenant.CommercialOwnerEmail) ||
                    IsMatchingEmail(profile.Email, cleanNotificationEmail))
                {
                    return "Auto-indicação detectada: o e-mail do parceiro comercial coincide com o titular do tenant indicado.";
                }
            }
        }

        return null;
    }

    private static bool IsMatchingEmail(string? email1, string? email2)
    {
        if (string.IsNullOrWhiteSpace(email1) || string.IsNullOrWhiteSpace(email2))
        {
            return false;
        }

        return string.Equals(email1.Trim(), email2.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static string CleanDigits(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return Regex.Replace(value, @"\D", "");
    }
}
