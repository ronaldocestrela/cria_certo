using System.Text.Json;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Enums;
using CriaCerto.Modules.Backoffice.Application.Telemetry;
using CriaCerto.Modules.Tenancy.Application.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Backoffice.Application.Features.Observability.EventHandlers;

public sealed class PaymentAlertEventHandler :
    INotificationHandler<PaymentInvoiceFailedIntegrationEvent>,
    INotificationHandler<WebhookSignatureFailedIntegrationEvent>
{
    private readonly DbContext _dbContext;

    public PaymentAlertEventHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(
        PaymentInvoiceFailedIntegrationEvent notification,
        CancellationToken cancellationToken)
    {
        var ruleCode = BackofficeAlertRules.PaymentInvoiceFailed;
        var severity = BackofficeAlertRules.GetDefaultSeverity(ruleCode);
        var fingerprint = $"{ruleCode}:{notification.TenantId}:{notification.InvoiceId}";

        var description = $"Fatura recorrente '{notification.InvoiceId}' no valor de {notification.Currency} {notification.AmountDue:N2} falhou no Stripe para o produtor '{notification.TenantName}'. Acesso colocado em PastDue. Intervenção de CS/Suporte recomendada para regularização antes do término do período de carência.";

        var contextJson = JsonSerializer.Serialize(new
        {
            tenantId = notification.TenantId,
            tenantName = notification.TenantName,
            invoiceId = notification.InvoiceId,
            stripeCustomerId = notification.StripeCustomerId,
            amountDue = notification.AmountDue,
            currency = notification.Currency,
            failureReason = notification.FailureReason,
            occurredAtUtc = notification.OccurredOnUtc
        });

        await CreateOrIncrementAlertAsync(
            ruleCode: ruleCode,
            title: BackofficeAlertRules.GetDefaultTitle(ruleCode),
            description: description,
            severity: severity,
            fingerprint: fingerprint,
            contextJson: contextJson,
            targetTenantId: notification.TenantId,
            targetTenantName: notification.TenantName,
            cancellationToken: cancellationToken);
    }

    public async Task Handle(
        WebhookSignatureFailedIntegrationEvent notification,
        CancellationToken cancellationToken)
    {
        var ruleCode = BackofficeAlertRules.WebhookSignatureInvalid;
        var severity = BackofficeAlertRules.GetDefaultSeverity(ruleCode);
        // Deduplica por janela horária para evitar avalanche de alertas sob ataque contínuo
        var hourlyBucket = notification.OccurredOnUtc.ToString("yyyyMMddHH");
        var fingerprint = $"{ruleCode}:{hourlyBucket}";

        var description = $"Falha de validação criptográfica HMAC-SHA256 no webhook Stripe ({notification.Reason}). Tamanho do payload: {notification.PayloadLength} bytes. Tipo de evento: {notification.EventType ?? "desconhecido"}. Possível requisição forjada (tentativa de ataque) ou rotação de STRIPE_WEBHOOK_SECRET necessária.";

        var contextJson = JsonSerializer.Serialize(new
        {
            reason = notification.Reason,
            eventType = notification.EventType,
            payloadLength = notification.PayloadLength,
            occurredAtUtc = notification.OccurredOnUtc
        });

        await CreateOrIncrementAlertAsync(
            ruleCode: ruleCode,
            title: BackofficeAlertRules.GetDefaultTitle(ruleCode),
            description: description,
            severity: severity,
            fingerprint: fingerprint,
            contextJson: contextJson,
            cancellationToken: cancellationToken);
    }

    private async Task CreateOrIncrementAlertAsync(
        string ruleCode,
        string title,
        string description,
        AlertSeverity severity,
        string fingerprint,
        string contextJson,
        Guid? targetTenantId = null,
        string? targetTenantName = null,
        CancellationToken cancellationToken = default)
    {
        var existingAlert = await _dbContext.Set<BackofficeAlert>()
            .FirstOrDefaultAsync(a => a.Fingerprint == fingerprint && a.Status != AlertStatus.Resolved, cancellationToken);

        if (existingAlert != null)
        {
            var incrementResult = existingAlert.IncrementOccurrence(contextJson);
            if (incrementResult.IsSuccess)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                BackofficeTelemetry.RecordAlert(ruleCode, severity.ToString());
            }
            return;
        }

        var createResult = BackofficeAlert.Create(
            ruleCode: ruleCode,
            title: title,
            description: description,
            severity: severity,
            fingerprint: fingerprint,
            contextJson: contextJson,
            targetTenantId: targetTenantId,
            targetTenantName: targetTenantName);

        if (createResult.IsSuccess)
        {
            _dbContext.Set<BackofficeAlert>().Add(createResult.Value);
            await _dbContext.SaveChangesAsync(cancellationToken);
            BackofficeTelemetry.RecordAlert(ruleCode, severity.ToString());
        }
    }
}
