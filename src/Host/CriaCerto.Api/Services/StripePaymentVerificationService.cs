using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Stripe;

namespace CriaCerto.Api.Services;

public sealed class StripePaymentVerificationService : IReferralPaymentVerificationService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<StripePaymentVerificationService> _logger;

    public StripePaymentVerificationService(
        IConfiguration configuration,
        ILogger<StripePaymentVerificationService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<Result<bool>> IsInvoiceRefundedOrDisputedAsync(
        string stripeInvoiceId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(stripeInvoiceId))
        {
            return Result.Success(false);
        }

        var apiKey = _configuration["STRIPE_API_KEY"]
            ?? _configuration["Stripe:ApiKey"]
            ?? StripeConfiguration.ApiKey;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("Chave da API do Stripe não configurada. Ignorando consulta remota de fatura {InvoiceId}.", stripeInvoiceId);
            return Result.Success(false);
        }

        try
        {
            var invoiceService = new InvoiceService();
            var invoice = await invoiceService.GetAsync(
                stripeInvoiceId,
                new InvoiceGetOptions(),
                new RequestOptions { ApiKey = apiKey },
                cancellationToken);

            if (invoice == null)
            {
                return Result.Success(false);
            }

            // Se a fatura foi marcada como cancelada (void) ou incobrável (uncollectible)
            if (invoice.Status is "void" or "uncollectible")
            {
                _logger.LogInformation("Fatura Stripe {InvoiceId} está com status '{Status}'.", stripeInvoiceId, invoice.Status);
                return Result.Success(true);
            }

            // Se houve emissão de notas de crédito pós-pagamento (estornos/reembolsos via Stripe Billing)
            if (invoice.PostPaymentCreditNotesAmount > 0)
            {
                _logger.LogInformation(
                    "Fatura Stripe {InvoiceId} possui notas de crédito pós-pagamento (PostPaymentCreditNotesAmount={Amount}).",
                    stripeInvoiceId, invoice.PostPaymentCreditNotesAmount);
                return Result.Success(true);
            }

            return Result.Success(false);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Erro de comunicação ao consultar fatura Stripe {InvoiceId}: {Message}", stripeInvoiceId, ex.Message);
            return Result.Success(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao verificar status da fatura Stripe {InvoiceId}.", stripeInvoiceId);
            return Result.Success(false);
        }
    }
}
