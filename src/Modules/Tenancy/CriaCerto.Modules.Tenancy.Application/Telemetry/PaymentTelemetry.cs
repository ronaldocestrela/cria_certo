using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace CriaCerto.Modules.Tenancy.Application.Telemetry;

public static class PaymentTelemetry
{
    public const string MeterName = "CriaCerto.Modules.Tenancy.Payments";
    public const string ActivitySourceName = "CriaCerto.Modules.Tenancy.Payments";
    public const string Version = "1.0.0";

    public static readonly Meter Meter = new(MeterName, Version);
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, Version);

    public static readonly Counter<long> WebhookEventsCounter = Meter.CreateCounter<long>(
        name: "payments.webhook.events.total",
        unit: "{event}",
        description: "Total de eventos de webhook do Stripe recebidos");

    public static readonly Counter<long> WebhookSignatureFailuresCounter = Meter.CreateCounter<long>(
        name: "payments.webhook.signature_failures.total",
        unit: "{failure}",
        description: "Total de falhas na validação de assinatura criptográfica de webhooks");

    public static readonly Counter<long> InvoiceFailuresCounter = Meter.CreateCounter<long>(
        name: "payments.invoice.failures.total",
        unit: "{failure}",
        description: "Total de falhas no débito ou liquidação de faturas de pagamento");

    public static readonly Histogram<double> WebhookProcessingDuration = Meter.CreateHistogram<double>(
        name: "payments.webhook.duration_ms",
        unit: "ms",
        description: "Latência de processamento de webhooks do Stripe em milissegundos");

    public static void RecordWebhookEvent(string eventType, string status)
    {
        WebhookEventsCounter.Add(1,
            new KeyValuePair<string, object?>("event_type", eventType),
            new KeyValuePair<string, object?>("status", status));
    }

    public static void RecordSignatureFailure(string reason)
    {
        WebhookSignatureFailuresCounter.Add(1,
            new KeyValuePair<string, object?>("reason", reason));
    }

    public static void RecordInvoiceFailure(string? currency, string? reason, long? amountDue = null)
    {
        InvoiceFailuresCounter.Add(1,
            new KeyValuePair<string, object?>("currency", currency ?? "BRL"),
            new KeyValuePair<string, object?>("reason", reason ?? "payment_failed"),
            new KeyValuePair<string, object?>("amount_due", amountDue ?? 0));
    }

    public static void RecordWebhookDuration(string eventType, bool isSuccess, double durationMs)
    {
        WebhookProcessingDuration.Record(durationMs,
            new KeyValuePair<string, object?>("event_type", eventType),
            new KeyValuePair<string, object?>("is_success", isSuccess));
    }
}
