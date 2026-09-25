using CriaCerto.Modules.Tenancy.Application.Domain;

namespace CriaCerto.Modules.Tenancy.Application.Abstractions;

public sealed record CheckoutSessionResult(string SessionId, string Url);

public sealed record CustomerPortalSessionResult(string Url);

public sealed record StripeWebhookResult(bool Success, string? EventType, string? Message);

public interface IStripePaymentService
{
    Task<string> GetOrCreateCustomerAsync(Tenant tenant, User user, CancellationToken cancellationToken = default);

    Task<CheckoutSessionResult> CreateCheckoutSessionAsync(
        Tenant tenant,
        User user,
        string planName,
        string billingCycle,
        decimal unitAmount,
        string successUrl,
        string cancelUrl,
        string? priceId = null,
        CancellationToken cancellationToken = default);

    Task<CustomerPortalSessionResult> CreateCustomerPortalSessionAsync(
        Tenant tenant,
        string returnUrl,
        CancellationToken cancellationToken = default);

    Task<StripeWebhookResult> ProcessWebhookAsync(
        string jsonPayload,
        string stripeSignatureHeader,
        CancellationToken cancellationToken = default);
}
