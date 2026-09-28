using CriaCerto.BuildingBlocks.Abstractions.Licensing;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.BillingPortal;
using Stripe.Checkout;

namespace CriaCerto.Modules.Tenancy.Infrastructure.Services;

public sealed class StripePaymentService : IStripePaymentService
{
    private readonly ITenancyDbContext _dbContext;
    private readonly StripeOptions _options;
    private readonly ILogger<StripePaymentService> _logger;

    public StripePaymentService(
        ITenancyDbContext dbContext,
        IOptions<StripeOptions> options,
        ILogger<StripePaymentService> logger)
    {
        _dbContext = dbContext;
        _options = options.Value;
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            StripeConfiguration.ApiKey = _options.ApiKey;
        }
        else
        {
            _logger.LogWarning("Chave do Stripe (STRIPE_API_KEY) não configurada. Operações de pagamento falharão.");
        }
    }

    private void EnsureApiKeyConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey) && string.IsNullOrWhiteSpace(StripeConfiguration.ApiKey))
        {
            throw new InvalidOperationException("A chave de API da Stripe (STRIPE_API_KEY / Stripe:ApiKey) não está configurada.");
        }

        if (string.IsNullOrWhiteSpace(StripeConfiguration.ApiKey))
        {
            StripeConfiguration.ApiKey = _options.ApiKey;
        }
    }

    public async Task<string> GetOrCreateCustomerAsync(
        Tenant tenant,
        User user,
        CancellationToken cancellationToken = default)
    {
        EnsureApiKeyConfigured();

        if (!string.IsNullOrWhiteSpace(tenant.StripeCustomerId))
        {
            return tenant.StripeCustomerId;
        }

        var customerService = new CustomerService();
        var customerOptions = new CustomerCreateOptions
        {
            Name = tenant.Name,
            Email = user.Email,
            Description = $"Fazenda {tenant.Name} (CNPJ: {tenant.CNPJ})",
            Metadata = new Dictionary<string, string>
            {
                { "TenantId", tenant.Id.ToString() },
                { "CNPJ", tenant.CNPJ },
                { "UserId", user.Id.ToString() }
            }
        };

        var customer = await customerService.CreateAsync(customerOptions, cancellationToken: cancellationToken);

        tenant.StripeCustomerId = customer.Id;
        tenant.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Cliente Stripe criado: {CustomerId} para o Tenant {TenantId}", customer.Id, tenant.Id);
        return customer.Id;
    }

    public async Task<CheckoutSessionResult> CreateCheckoutSessionAsync(
        Tenant tenant,
        User user,
        string planId,
        string planName,
        string billingCycle,
        decimal unitAmount,
        string successUrl,
        string cancelUrl,
        string? priceId = null,
        CancellationToken cancellationToken = default)
    {
        var customerId = await GetOrCreateCustomerAsync(tenant, user, cancellationToken);

        var finalSuccessUrl = !string.IsNullOrWhiteSpace(successUrl)
            ? successUrl
            : _options.SuccessUrl;

        var finalCancelUrl = !string.IsNullOrWhiteSpace(cancelUrl)
            ? cancelUrl
            : _options.CancelUrl;

        var sessionOptions = new Stripe.Checkout.SessionCreateOptions
        {
            Customer = customerId,
            Mode = "subscription",
            SuccessUrl = finalSuccessUrl,
            CancelUrl = finalCancelUrl,
            Metadata = new Dictionary<string, string>
            {
                { "TenantId", tenant.Id.ToString() },
                { "PlanId", planId },
                { "PlanName", planName },
                { "BillingCycle", billingCycle }
            },
            SubscriptionData = new Stripe.Checkout.SessionSubscriptionDataOptions
            {
                Metadata = new Dictionary<string, string>
                {
                    { "TenantId", tenant.Id.ToString() },
                    { "PlanId", planId },
                    { "PlanName", planName },
                    { "BillingCycle", billingCycle }
                }
            }
        };

        if (!string.IsNullOrWhiteSpace(priceId))
        {
            sessionOptions.LineItems = new List<SessionLineItemOptions>
            {
                new()
                {
                    Price = priceId,
                    Quantity = 1
                }
            };
        }
        else
        {
            var interval = billingCycle.Contains("year", StringComparison.OrdinalIgnoreCase) ||
                           billingCycle.Contains("anual", StringComparison.OrdinalIgnoreCase)
                ? "year"
                : "month";

            sessionOptions.LineItems = new List<SessionLineItemOptions>
            {
                new()
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = "brl",
                        UnitAmount = (long)(unitAmount * 100),
                        Recurring = new SessionLineItemPriceDataRecurringOptions
                        {
                            Interval = interval
                        },
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = $"CriaCerto {planName} ({interval})",
                            Description = $"Assinatura plano {planName} da plataforma CriaCerto Bovino SaaS."
                        }
                    },
                    Quantity = 1
                }
            };
        }

        var sessionService = new Stripe.Checkout.SessionService();
        var session = await sessionService.CreateAsync(sessionOptions, cancellationToken: cancellationToken);

        _logger.LogInformation("Checkout Session criada no Stripe: {SessionId} para Tenant {TenantId}", session.Id, tenant.Id);
        return new CheckoutSessionResult(session.Id, session.Url);
    }

    public async Task<CustomerPortalSessionResult> CreateCustomerPortalSessionAsync(
        Tenant tenant,
        string returnUrl,
        CancellationToken cancellationToken = default)
    {
        EnsureApiKeyConfigured();

        if (string.IsNullOrWhiteSpace(tenant.StripeCustomerId))
        {
            throw new InvalidOperationException("Tenant não possui identificador de cliente registrado no Stripe.");
        }

        var finalReturnUrl = !string.IsNullOrWhiteSpace(returnUrl)
            ? returnUrl
            : _options.ReturnUrl;

        var portalOptions = new Stripe.BillingPortal.SessionCreateOptions
        {
            Customer = tenant.StripeCustomerId,
            ReturnUrl = finalReturnUrl
        };

        var portalService = new Stripe.BillingPortal.SessionService();
        var portalSession = await portalService.CreateAsync(portalOptions, cancellationToken: cancellationToken);

        return new CustomerPortalSessionResult(portalSession.Url);
    }

    public async Task<StripeWebhookResult> ProcessWebhookAsync(
        string jsonPayload,
        string stripeSignatureHeader,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.WebhookSecret))
        {
            _logger.LogCritical("Tentativa de processar webhook Stripe sem STRIPE_WEBHOOK_SECRET configurado. Rejeitando requisição por segurança.");
            return new StripeWebhookResult(false, null, "Webhook signature verification required: WebhookSecret is not configured.");
        }

        if (string.IsNullOrWhiteSpace(stripeSignatureHeader))
        {
            _logger.LogWarning("Webhook Stripe recebido sem o cabeçalho Stripe-Signature.");
            return new StripeWebhookResult(false, null, "Cabeçalho Stripe-Signature ausente ou inválido.");
        }

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(
                jsonPayload,
                stripeSignatureHeader,
                _options.WebhookSecret,
                throwOnApiVersionMismatch: false);
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Falha na validação criptográfica da assinatura do Webhook Stripe.");
            return new StripeWebhookResult(false, null, $"Assinatura inválida: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao validar assinatura do Webhook Stripe.");
            return new StripeWebhookResult(false, null, $"Falha na validação do webhook: {ex.Message}");
        }

        _logger.LogInformation("Processando webhook Stripe: {EventType} (Id: {EventId})", stripeEvent.Type, stripeEvent.Id);

        if (!string.IsNullOrWhiteSpace(stripeEvent.Id))
        {
            var alreadyProcessed = await _dbContext.StripeWebhookEvents
                .AnyAsync(e => e.EventId == stripeEvent.Id, cancellationToken);

            if (alreadyProcessed)
            {
                _logger.LogInformation(
                    "Evento Stripe {EventId} ({EventType}) já processado anteriormente. Ignorando reexecução (idempotência).",
                    stripeEvent.Id,
                    stripeEvent.Type);

                return new StripeWebhookResult(true, stripeEvent.Type, "Evento já processado anteriormente (idempotente).");
            }
        }

        try
        {
            switch (stripeEvent.Type)
            {
                case "checkout.session.completed":
                {
                    if (stripeEvent.Data.Object is Stripe.Checkout.Session session)
                    {
                        await HandleCheckoutSessionCompletedAsync(session, cancellationToken);
                    }
                    break;
                }

                case "invoice.paid":
                {
                    if (stripeEvent.Data.Object is Stripe.Invoice invoice)
                    {
                        await HandleInvoicePaidAsync(invoice, cancellationToken);
                    }
                    break;
                }

                case "invoice.payment_failed":
                {
                    if (stripeEvent.Data.Object is Stripe.Invoice invoice)
                    {
                        await HandleInvoicePaymentFailedAsync(invoice, cancellationToken);
                    }
                    break;
                }

                case "customer.subscription.updated":
                {
                    if (stripeEvent.Data.Object is Stripe.Subscription subscription)
                    {
                        await HandleSubscriptionUpdatedAsync(subscription, cancellationToken);
                    }
                    break;
                }

                case "customer.subscription.deleted":
                {
                    if (stripeEvent.Data.Object is Stripe.Subscription subscription)
                    {
                        await HandleSubscriptionDeletedAsync(subscription, cancellationToken);
                    }
                    break;
                }

                default:
                    _logger.LogDebug("Evento Stripe não tratado explicitamente: {EventType}", stripeEvent.Type);
                    break;
            }

            if (!string.IsNullOrWhiteSpace(stripeEvent.Id))
            {
                var webhookRecord = StripeWebhookEvent.Create(
                    stripeEvent.Id,
                    stripeEvent.Type ?? string.Empty,
                    jsonPayload);

                _dbContext.StripeWebhookEvents.Add(webhookRecord);

                try
                {
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException dbEx)
                {
                    _logger.LogWarning(dbEx, "Concorrência detectada ao salvar evento Stripe {EventId}. Tratando como idempotente.", stripeEvent.Id);
                }
            }

            return new StripeWebhookResult(true, stripeEvent.Type, "Processado com sucesso.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar evento Stripe {EventType}", stripeEvent.Type);
            return new StripeWebhookResult(false, stripeEvent.Type, ex.Message);
        }
    }

    private async Task HandleCheckoutSessionCompletedAsync(
        Stripe.Checkout.Session session,
        CancellationToken cancellationToken)
    {
        string? tenantIdStr = null;
        session.Metadata?.TryGetValue("TenantId", out tenantIdStr);

        Tenant? tenant = null;
        if (Guid.TryParse(tenantIdStr, out var tenantId))
        {
            tenant = await _dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
        }

        if (tenant == null && !string.IsNullOrWhiteSpace(session.CustomerId))
        {
            tenant = await _dbContext.Tenants.FirstOrDefaultAsync(t => t.StripeCustomerId == session.CustomerId, cancellationToken);
        }

        if (tenant == null)
        {
            _logger.LogWarning("CheckoutSessionCompleted: Tenant não encontrado para session {SessionId}", session.Id);
            return;
        }

        tenant.StripeCustomerId = session.CustomerId;
        tenant.StripeSubscriptionId = session.SubscriptionId;

        string? rawPlan = null;
        if (session.Metadata != null)
        {
            if (session.Metadata.TryGetValue("PlanId", out var pid) && !string.IsNullOrWhiteSpace(pid))
            {
                rawPlan = pid;
            }
            else if (session.Metadata.TryGetValue("PlanName", out var pname) && !string.IsNullOrWhiteSpace(pname))
            {
                rawPlan = pname;
            }
        }

        if (!string.IsNullOrWhiteSpace(rawPlan))
        {
            var canonicalPlan = ModuleLicenseChecker.NormalizePlan(rawPlan);
            tenant.SubscribedPlan = canonicalPlan;
            AdjustTenantCapacityForPlan(tenant, canonicalPlan);
        }

        tenant.Status = "Active";
        tenant.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Tenant {TenantId} ativado após Checkout bem-sucedido no Stripe.", tenant.Id);
    }

    private async Task HandleInvoicePaidAsync(
        Stripe.Invoice invoice,
        CancellationToken cancellationToken)
    {
        var tenant = await _dbContext.Tenants.FirstOrDefaultAsync(
            t => t.StripeCustomerId == invoice.CustomerId,
            cancellationToken);

        if (tenant == null)
        {
            _logger.LogWarning("InvoicePaid: Tenant não encontrado para cliente {CustomerId}", invoice.CustomerId);
            return;
        }

        tenant.Status = "Active";
        tenant.StatusReason = null;
        tenant.StatusChangedAtUtc = DateTime.UtcNow;

        var line = invoice.Lines?.Data?.FirstOrDefault();
        if (line?.Period?.End != null)
        {
            tenant.CurrentPeriodEndUtc = line.Period.End;
        }

        tenant.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Fatura quitada para o Tenant {TenantId}. Renovação garantida até {PeriodEnd}", tenant.Id, tenant.CurrentPeriodEndUtc);
    }

    private async Task HandleInvoicePaymentFailedAsync(
        Stripe.Invoice invoice,
        CancellationToken cancellationToken)
    {
        var tenant = await _dbContext.Tenants.FirstOrDefaultAsync(
            t => t.StripeCustomerId == invoice.CustomerId,
            cancellationToken);

        if (tenant == null) return;

        tenant.Status = "PastDue";
        tenant.StatusReason = "Falha no pagamento da fatura recorrente via Stripe.";
        tenant.StatusChangedAtUtc = DateTime.UtcNow;
        tenant.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogWarning("Tenant {TenantId} colocado em PastDue devido a falha no pagamento.", tenant.Id);
    }

    private async Task HandleSubscriptionUpdatedAsync(
        Stripe.Subscription subscription,
        CancellationToken cancellationToken)
    {
        var tenant = await _dbContext.Tenants.FirstOrDefaultAsync(
            t => t.StripeSubscriptionId == subscription.Id || t.StripeCustomerId == subscription.CustomerId,
            cancellationToken);

        if (tenant == null) return;

        tenant.CancelAtPeriodEnd = subscription.CancelAtPeriodEnd;
        var firstItem = subscription.Items?.Data?.FirstOrDefault();
        if (firstItem?.Price != null)
        {
            tenant.StripePriceId = firstItem.Price.Id;
        }

        string? rawUpdatedPlan = null;
        if (subscription.Metadata != null)
        {
            if (subscription.Metadata.TryGetValue("PlanId", out var pid) && !string.IsNullOrWhiteSpace(pid))
            {
                rawUpdatedPlan = pid;
            }
            else if (subscription.Metadata.TryGetValue("PlanName", out var pname) && !string.IsNullOrWhiteSpace(pname))
            {
                rawUpdatedPlan = pname;
            }
        }

        if (!string.IsNullOrWhiteSpace(rawUpdatedPlan))
        {
            var canonicalPlan = ModuleLicenseChecker.NormalizePlan(rawUpdatedPlan);
            tenant.SubscribedPlan = canonicalPlan;
            AdjustTenantCapacityForPlan(tenant, canonicalPlan);
        }

        switch (subscription.Status)
        {
            case "active":
                tenant.Status = "Active";
                break;
            case "past_due":
                tenant.Status = "PastDue";
                tenant.StatusReason = "Assinatura Stripe está com pendência de pagamento (past_due).";
                break;
            case "canceled":
            case "unpaid":
                tenant.Status = "Suspended";
                tenant.StatusReason = $"Assinatura Stripe suspensa ({subscription.Status}).";
                break;
        }

        tenant.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task HandleSubscriptionDeletedAsync(
        Stripe.Subscription subscription,
        CancellationToken cancellationToken)
    {
        var tenant = await _dbContext.Tenants.FirstOrDefaultAsync(
            t => t.StripeSubscriptionId == subscription.Id || t.StripeCustomerId == subscription.CustomerId,
            cancellationToken);

        if (tenant == null) return;

        tenant.Status = "Cancelled";
        tenant.StatusReason = "Assinatura cancelada no Stripe.";
        tenant.StatusChangedAtUtc = DateTime.UtcNow;
        tenant.CancelAtPeriodEnd = false;
        tenant.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Assinatura do Tenant {TenantId} foi cancelada.", tenant.Id);
    }

    private static void AdjustTenantCapacityForPlan(Tenant tenant, string planName)
    {
        var plan = ModuleLicenseChecker.NormalizePlan(planName);
        if (string.Equals(plan, ModuleLicenseChecker.StarterPlan, StringComparison.OrdinalIgnoreCase))
        {
            tenant.Capacity = 500;
        }
        else if (string.Equals(plan, ModuleLicenseChecker.ProPlan, StringComparison.OrdinalIgnoreCase))
        {
            tenant.Capacity = 2500;
        }
        else if (string.Equals(plan, ModuleLicenseChecker.EnterprisePlan, StringComparison.OrdinalIgnoreCase))
        {
            tenant.Capacity = 100000;
        }
    }
}
