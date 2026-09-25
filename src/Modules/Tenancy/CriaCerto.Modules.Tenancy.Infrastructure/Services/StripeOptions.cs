namespace CriaCerto.Modules.Tenancy.Infrastructure.Services;

public sealed class StripeOptions
{
    public const string SectionName = "Stripe";

    public string ApiKey { get; set; } = string.Empty;
    public string PublishableKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string SuccessUrl { get; set; } = "http://localhost:8081/settings/subscription?success=true";
    public string CancelUrl { get; set; } = "http://localhost:8081/settings/subscription?canceled=true";
    public string ReturnUrl { get; set; } = "http://localhost:8081/settings/subscription";
}
