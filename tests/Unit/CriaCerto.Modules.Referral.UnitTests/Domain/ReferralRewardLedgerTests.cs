using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Domain;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Domain.Errors;
using FluentAssertions;
using Xunit;

namespace CriaCerto.Modules.Referral.UnitTests.Domain;

public class ReferralRewardLedgerTests
{
    [Fact]
    public void Create_CashCommission_PendingMaturity_ShouldSucceed()
    {
        // Arrange
        var attributionId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var maturityDate = DateTime.UtcNow.AddDays(30);

        // Act
        var result = ReferralRewardLedger.CreateCashCommission(
            attributionId,
            participantId,
            grossAmount: 150m,
            netAmount: 150m,
            currency: "BRL",
            maturityDateUtc: maturityDate,
            stripeInvoiceId: "in_123456");

        // Assert
        result.IsSuccess.Should().BeTrue();
        var ledger = result.Value;
        ledger.ReferralAttributionId.Should().Be(attributionId);
        ledger.ParticipantId.Should().Be(participantId);
        ledger.RewardType.Should().Be(RewardType.CashCommission);
        ledger.GrossAmount.Should().Be(150m);
        ledger.NetAmount.Should().Be(150m);
        ledger.Currency.Should().Be("BRL");
        ledger.Status.Should().Be(LedgerRewardStatus.PendingMaturity);
        ledger.MaturityDateUtc.Should().Be(maturityDate);
        ledger.StripeInvoiceId.Should().Be("in_123456");
        ledger.CancellationReason.Should().BeNull();
    }

    [Fact]
    public void Create_InvoiceDiscountCredit_ShouldSucceed()
    {
        // Act
        var result = ReferralRewardLedger.CreateInvoiceDiscountCredit(
            Guid.NewGuid(),
            Guid.NewGuid(),
            discountAmount: 50m,
            currency: "BRL",
            stripeInvoiceId: "in_999888");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.RewardType.Should().Be(RewardType.InvoiceDiscountCredit);
        result.Value.GrossAmount.Should().Be(50m);
        result.Value.NetAmount.Should().Be(50m);
    }

    [Fact]
    public void ReleaseForPayout_WhenPendingMaturity_ShouldTransitionToAvailable()
    {
        // Arrange
        var ledger = ReferralRewardLedger.CreateCashCommission(
            Guid.NewGuid(),
            Guid.NewGuid(),
            grossAmount: 100m,
            netAmount: 100m,
            currency: "BRL",
            maturityDateUtc: DateTime.UtcNow.AddDays(-1)).Value;

        // Act
        var result = ledger.ReleaseForPayout();

        // Assert
        result.IsSuccess.Should().BeTrue();
        ledger.Status.Should().Be(LedgerRewardStatus.AvailableForPayout);
    }

    [Fact]
    public void MarkAppliedToInvoice_WhenPendingOrAvailable_ShouldSucceed()
    {
        // Arrange
        var ledger = ReferralRewardLedger.CreateInvoiceDiscountCredit(
            Guid.NewGuid(),
            Guid.NewGuid(),
            50m,
            "BRL",
            "in_123").Value;

        // Act
        var result = ledger.MarkAppliedToInvoice("cbtxn_stripe_987");

        // Assert
        result.IsSuccess.Should().BeTrue();
        ledger.Status.Should().Be(LedgerRewardStatus.AppliedToInvoice);
        ledger.StripeCreditBalanceTransactionId.Should().Be("cbtxn_stripe_987");
    }

    [Fact]
    public void MarkPaidOut_WhenAvailableForPayout_ShouldSucceed()
    {
        // Arrange
        var ledger = ReferralRewardLedger.CreateCashCommission(
            Guid.NewGuid(),
            Guid.NewGuid(),
            100m,
            100m,
            "BRL",
            DateTime.UtcNow.AddDays(-1)).Value;

        ledger.ReleaseForPayout();

        // Act
        var result = ledger.MarkPaidOut("PIX_E2E_998877665544");

        // Assert
        result.IsSuccess.Should().BeTrue();
        ledger.Status.Should().Be(LedgerRewardStatus.PaidOut);
    }

    [Fact]
    public void Cancel_WhenPending_ShouldSetStatusToCancelledAndReason()
    {
        // Arrange
        var ledger = ReferralRewardLedger.CreateCashCommission(
            Guid.NewGuid(),
            Guid.NewGuid(),
            200m,
            200m,
            "BRL",
            DateTime.UtcNow.AddDays(30)).Value;

        // Act
        var result = ledger.Cancel("Estorno de pagamento pelo cliente no Stripe (charge.refunded)");

        // Assert
        result.IsSuccess.Should().BeTrue();
        ledger.Status.Should().Be(LedgerRewardStatus.Cancelled);
        ledger.CancellationReason.Should().Be("Estorno de pagamento pelo cliente no Stripe (charge.refunded)");
    }

    [Fact]
    public void Cancel_WhenAlreadyPaidOut_ShouldFail()
    {
        // Arrange
        var ledger = ReferralRewardLedger.CreateCashCommission(
            Guid.NewGuid(),
            Guid.NewGuid(),
            100m,
            100m,
            "BRL",
            DateTime.UtcNow.AddDays(-1)).Value;

        ledger.ReleaseForPayout();
        ledger.MarkPaidOut("COMPROVANTE_123");

        // Act
        var result = ledger.Cancel("Tentativa de estorno");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ReferralRewardLedger.CannotCancelPaidOut");
    }
}
