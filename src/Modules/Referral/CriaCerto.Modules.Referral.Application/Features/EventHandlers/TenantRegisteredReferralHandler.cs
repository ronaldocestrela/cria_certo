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
    private readonly ILogger<TenantRegisteredReferralHandler>? _logger;

    public TenantRegisteredReferralHandler(
        IReferralDbContext dbContext,
        ILogger<TenantRegisteredReferralHandler>? logger = null)
    {
        _dbContext = dbContext;
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

        // Detecção de auto-indicação: produtor indicando a si mesmo / mesma fazenda
        if (participant.ParticipantType == ParticipantType.Customer &&
            participant.TenantId.HasValue &&
            participant.TenantId.Value == notification.TenantId)
        {
            _logger?.LogWarning("Tentativa de auto-indicação detectada para Tenant {TenantId} e código '{ReferralCode}'.",
                notification.TenantId, notification.ReferralCode);

            var fraudAttribution = ReferralAttribution.Create(
                referralCodeId: referralCode.Id,
                referrerParticipantId: participant.Id,
                referredTenantId: notification.TenantId,
                originIpAddress: notification.OriginIpAddress,
                userAgent: notification.UserAgent);

            if (fraudAttribution.IsSuccess)
            {
                fraudAttribution.Value.FlagAsFraud("Auto-indicação detectada: o participante indicador pertence à mesma fazenda indicada.");
                _dbContext.Attributions.Add(fraudAttribution.Value);
                await _dbContext.SaveChangesAsync(cancellationToken);
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
}
