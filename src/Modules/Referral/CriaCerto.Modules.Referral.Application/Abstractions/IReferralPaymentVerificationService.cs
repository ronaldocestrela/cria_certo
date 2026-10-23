using CriaCerto.BuildingBlocks.Abstractions.Results;

namespace CriaCerto.Modules.Referral.Application.Abstractions;

public interface IReferralPaymentVerificationService
{
    /// <summary>
    /// Verifica preventivamente se uma fatura do Stripe foi estornada (refunded) ou contestada (disputed).
    /// </summary>
    Task<Result<bool>> IsInvoiceRefundedOrDisputedAsync(string stripeInvoiceId, CancellationToken cancellationToken = default);
}
