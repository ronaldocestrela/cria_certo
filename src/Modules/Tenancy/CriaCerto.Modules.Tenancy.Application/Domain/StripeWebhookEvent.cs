namespace CriaCerto.Modules.Tenancy.Application.Domain;

public sealed class StripeWebhookEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EventId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public DateTime ProcessedAtUtc { get; set; } = DateTime.UtcNow;
    public string PayloadJson { get; set; } = string.Empty;

    public static StripeWebhookEvent Create(
        string eventId,
        string eventType,
        string payloadJson)
    {
        return new StripeWebhookEvent
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            EventType = eventType,
            ProcessedAtUtc = DateTime.UtcNow,
            PayloadJson = payloadJson
        };
    }
}
