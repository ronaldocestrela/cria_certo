using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Domain;

namespace CriaCerto.Modules.Tenancy.Application.Abstractions;

public sealed record CheckoutSessionResult(string SessionId, string Url);

public sealed record CustomerPortalSessionResult(string Url);

public sealed record StripeWebhookResult(bool Success, string? EventType, string? Message);

public sealed record StripeModularLineItem(
    string ModuleKey,
    string ModuleName,
    decimal UnitAmount,
    string Interval,
    string? PriceId = null
);

public sealed record StripeModularSubscriptionUpdateResult(
    string SubscriptionId,
    string Status,
    IReadOnlyDictionary<string, string> SubscriptionItemIdsByModule
);

public interface IStripePaymentService
{
    Task<string> GetOrCreateCustomerAsync(Tenant tenant, User user, CancellationToken cancellationToken = default);

    Task<CheckoutSessionResult> CreateCheckoutSessionAsync(
        Tenant tenant,
        User user,
        string planId,
        string planName,
        string billingCycle,
        decimal unitAmount,
        string successUrl,
        string cancelUrl,
        string? priceId = null,
        CancellationToken cancellationToken = default);

    Task<CheckoutSessionResult> CreateModularCheckoutSessionAsync(
        Tenant tenant,
        User user,
        string headTierCode,
        int maxHeads,
        IReadOnlyList<string> subscribedModules,
        string billingCycle,
        IReadOnlyList<StripeModularLineItem> lineItems,
        string successUrl,
        string cancelUrl,
        string? planId = null,
        string? planName = null,
        CancellationToken cancellationToken = default);

    Task<CustomerPortalSessionResult> CreateCustomerPortalSessionAsync(
        Tenant tenant,
        string returnUrl,
        CancellationToken cancellationToken = default);

    Task<StripeWebhookResult> ProcessWebhookAsync(
        string jsonPayload,
        string stripeSignatureHeader,
        CancellationToken cancellationToken = default);

    Task<Result> UpdateSubscriptionBillingDateAsync(
        string subscriptionId,
        DateTime newBillingDateUtc,
        CancellationToken cancellationToken = default);

    Task<Result<StripeModularSubscriptionUpdateResult>> UpdateModularSubscriptionAsync(
        Tenant tenant,
        string headTierCode,
        int maxHeads,
        IReadOnlyList<string> subscribedModules,
        string billingCycle,
        IReadOnlyList<StripeModularLineItem> lineItems,
        CancellationToken cancellationToken = default);
}
