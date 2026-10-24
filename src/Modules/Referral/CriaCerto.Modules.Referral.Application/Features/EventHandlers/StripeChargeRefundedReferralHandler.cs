using System.Text.Json;
using CriaCerto.Modules.Referral.Application.Abstractions;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Tenancy.Application.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CriaCerto.Modules.Referral.Application.Features.EventHandlers;

public sealed class StripeChargeRefundedReferralHandler : INotificationHandler<StripeChargeRefundedIntegrationEvent>
{
    private readonly IReferralDbContext _dbContext;
    private readonly IReferralAuditSink? _auditSink;
    private readonly ILogger<StripeChargeRefundedReferralHandler>? _logger;

    public StripeChargeRefundedReferralHandler(
        IReferralDbContext dbContext,
        IReferralAuditSink? auditSink = null,
        ILogger<StripeChargeRefundedReferralHandler>? logger = null)
    {
        _dbContext = dbContext;
        _auditSink = auditSink;
        _logger = logger;
    }

    public async Task Handle(StripeChargeRefundedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(notification.StripeInvoiceId))
        {
            _logger?.LogDebug("Evento StripeChargeRefunded recebido sem StripeInvoiceId. Ignorando cancelamento de comissão.");
            return;
        }

        var pendingLedgers = await _dbContext.RewardLedger
            .Where(l => l.StripeInvoiceId == notification.StripeInvoiceId
                     && l.Status == LedgerRewardStatus.PendingMaturity)
            .ToListAsync(cancellationToken);

        if (pendingLedgers.Count == 0)
        {
            _logger?.LogDebug("Nenhum lançamento pendente encontrado para a fatura estornada {InvoiceId}.", notification.StripeInvoiceId);
            return;
        }

        var reason = $"Estorno de pagamento recebido via webhook Stripe (ChargeId: {notification.StripeChargeId}, Estornado: R$ {notification.AmountRefunded:N2}).";

        foreach (var ledger in pendingLedgers)
        {
            var cancelResult = ledger.Cancel(reason);
            if (cancelResult.IsSuccess)
            {
                _logger?.LogInformation("Lançamento de comissão {LedgerId} cancelado compulsoriamente devido a estorno na fatura {InvoiceId}.",
                    ledger.Id, notification.StripeInvoiceId);

                if (_auditSink != null)
                {
                    var auditPayload = JsonSerializer.Serialize(new
                    {
                        LedgerId = ledger.Id,
                        ledger.ParticipantId,
                        ledger.NetAmount,
                        ledger.StripeInvoiceId,
                        notification.StripeChargeId,
                        notification.AmountRefunded,
                        Reason = reason
                    });

                    await _auditSink.RecordSystemAuditAsync(
                        action: "Referral.CommissionCancelled.WebhookRefund",
                        resource: $"RewardLedger/{ledger.Id}",
                        targetTenantId: null,
                        detailsJson: auditPayload,
                        cancellationToken: cancellationToken);
                }
            }
            else
            {
                _logger?.LogWarning("Falha ao cancelar lançamento {LedgerId} após estorno: {Error}",
                    ledger.Id, cancelResult.Error.Message);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
