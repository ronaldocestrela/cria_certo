using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Domain.Errors;

namespace CriaCerto.Modules.Referral.Application.Domain;

public sealed class ReferralRewardLedger
{
    public Guid Id { get; private set; }
    public Guid ReferralAttributionId { get; private set; }
    public Guid ParticipantId { get; private set; }
    public RewardType RewardType { get; private set; }
    public decimal GrossAmount { get; private set; }
    public decimal NetAmount { get; private set; }
    public string Currency { get; private set; } = "BRL";
    public LedgerRewardStatus Status { get; private set; }
    public DateTime? MaturityDateUtc { get; private set; }
    public string? StripeInvoiceId { get; private set; }
    public string? StripeCreditBalanceTransactionId { get; private set; }
    public string? PayoutTransactionReference { get; private set; }
    public string? PayoutReceiptFilePath { get; private set; }
    public DateTime? PayoutRegisteredAtUtc { get; private set; }
    public Guid? PayoutRegisteredByAdminId { get; private set; }
    public string? CancellationReason { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    private ReferralRewardLedger() { }

    public static Result<ReferralRewardLedger> CreateCashCommission(
        Guid referralAttributionId,
        Guid participantId,
        decimal grossAmount,
        decimal netAmount,
        string currency,
        DateTime maturityDateUtc,
        string? stripeInvoiceId = null)
    {
        if (referralAttributionId == Guid.Empty)
            return Result.Failure<ReferralRewardLedger>(Error.Validation("ReferralRewardLedger.AttributionRequired", "A atribuição de indicação é obrigatória."));

        if (participantId == Guid.Empty)
            return Result.Failure<ReferralRewardLedger>(Error.Validation("ReferralRewardLedger.ParticipantRequired", "O participante é obrigatório."));

        if (grossAmount <= 0 || netAmount <= 0)
            return Result.Failure<ReferralRewardLedger>(ReferralErrors.Ledger.InvalidAmount);

        var ledger = new ReferralRewardLedger
        {
            Id = Guid.NewGuid(),
            ReferralAttributionId = referralAttributionId,
            ParticipantId = participantId,
            RewardType = RewardType.CashCommission,
            GrossAmount = grossAmount,
            NetAmount = netAmount,
            Currency = string.IsNullOrWhiteSpace(currency) ? "BRL" : currency.Trim().ToUpperInvariant(),
            Status = LedgerRewardStatus.PendingMaturity,
            MaturityDateUtc = maturityDateUtc,
            StripeInvoiceId = stripeInvoiceId,
            CreatedAtUtc = DateTime.UtcNow
        };

        return Result.Success(ledger);
    }

    public static Result<ReferralRewardLedger> CreateInvoiceDiscountCredit(
        Guid referralAttributionId,
        Guid participantId,
        decimal discountAmount,
        string currency = "BRL",
        string? stripeInvoiceId = null,
        decimal? maxInvoiceAmount = null)
    {
        if (referralAttributionId == Guid.Empty)
            return Result.Failure<ReferralRewardLedger>(Error.Validation("ReferralRewardLedger.AttributionRequired", "A atribuição de indicação é obrigatória."));

        if (participantId == Guid.Empty)
            return Result.Failure<ReferralRewardLedger>(Error.Validation("ReferralRewardLedger.ParticipantRequired", "O participante é obrigatório."));

        if (discountAmount <= 0)
            return Result.Failure<ReferralRewardLedger>(ReferralErrors.Ledger.InvalidAmount);

        var effectiveDiscount = maxInvoiceAmount.HasValue && maxInvoiceAmount.Value > 0
            ? Math.Min(discountAmount, maxInvoiceAmount.Value)
            : discountAmount;

        var ledger = new ReferralRewardLedger
        {
            Id = Guid.NewGuid(),
            ReferralAttributionId = referralAttributionId,
            ParticipantId = participantId,
            RewardType = RewardType.InvoiceDiscountCredit,
            GrossAmount = effectiveDiscount,
            NetAmount = effectiveDiscount,
            Currency = string.IsNullOrWhiteSpace(currency) ? "BRL" : currency.Trim().ToUpperInvariant(),
            Status = LedgerRewardStatus.PendingMaturity,
            MaturityDateUtc = null,
            StripeInvoiceId = stripeInvoiceId,
            CreatedAtUtc = DateTime.UtcNow
        };

        return Result.Success(ledger);
    }

    public Result ReleaseForPayout()
    {
        if (Status != LedgerRewardStatus.PendingMaturity)
            return Result.Failure(ReferralErrors.Ledger.InvalidStateTransition);

        Status = LedgerRewardStatus.AvailableForPayout;
        UpdatedAtUtc = DateTime.UtcNow;

        return Result.Success();
    }

    public Result MarkAppliedToInvoice(string stripeCreditBalanceTransactionId)
    {
        if (Status == LedgerRewardStatus.Cancelled || Status == LedgerRewardStatus.PaidOut)
            return Result.Failure(ReferralErrors.Ledger.InvalidStateTransition);

        Status = LedgerRewardStatus.AppliedToInvoice;
        StripeCreditBalanceTransactionId = stripeCreditBalanceTransactionId;
        UpdatedAtUtc = DateTime.UtcNow;

        return Result.Success();
    }

    public Result MarkPaidOut(
        string transactionReference,
        string? receiptFilePath = null,
        Guid? registeredByAdminId = null,
        DateTime? registeredAtUtc = null)
    {
        if (Status != LedgerRewardStatus.AvailableForPayout)
            return Result.Failure(ReferralErrors.Ledger.InvalidStateTransition);

        if (string.IsNullOrWhiteSpace(transactionReference))
            return Result.Failure(ReferralErrors.Ledger.TransactionReferenceRequired);

        Status = LedgerRewardStatus.PaidOut;
        PayoutTransactionReference = transactionReference.Trim();
        PayoutReceiptFilePath = receiptFilePath?.Trim();
        PayoutRegisteredByAdminId = registeredByAdminId;
        PayoutRegisteredAtUtc = registeredAtUtc ?? DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;

        return Result.Success();
    }

    public Result Cancel(string reason)
    {
        if (Status == LedgerRewardStatus.PaidOut)
            return Result.Failure(ReferralErrors.Ledger.CannotCancelPaidOut);

        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure(ReferralErrors.Ledger.CancellationReasonRequired);

        Status = LedgerRewardStatus.Cancelled;
        CancellationReason = reason.Trim();
        UpdatedAtUtc = DateTime.UtcNow;

        return Result.Success();
    }
}
