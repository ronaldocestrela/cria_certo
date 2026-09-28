using MediatR;

namespace CriaCerto.Modules.Tenancy.Application.Events;

public sealed record PaymentInvoiceFailedIntegrationEvent(
    Guid TenantId,
    string TenantName,
    string InvoiceId,
    string? StripeCustomerId,
    decimal AmountDue,
    string Currency,
    string? FailureReason,
    DateTime OccurredOnUtc
) : INotification;

public sealed record WebhookSignatureFailedIntegrationEvent(
    string Reason,
    string? EventType,
    int PayloadLength,
    DateTime OccurredOnUtc
) : INotification;
