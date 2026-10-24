using CriaCerto.BuildingBlocks.Abstractions.Results;

namespace CriaCerto.Modules.Referral.Application.Abstractions;

public interface IReferralPaymentVerificationService
{
    /// <summary>
    /// Verifica preventivamente se uma fatura do Stripe foi estornada (refunded) ou contestada (disputed).
    /// </summary>
    Task<Result<bool>> IsInvoiceRefundedOrDisputedAsync(string stripeInvoiceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica se o cartão de crédito do tenant pagante coincide (mesmo fingerprint) com cartões associados ao indicador no Stripe.
    /// </summary>
    Task<Result<bool>> HasCardFingerprintCollisionAsync(
        string payingStripeCustomerId,
        string referrerStripeCustomerId,
        string? directPayingFingerprint = null,
        CancellationToken cancellationToken = default);
}
