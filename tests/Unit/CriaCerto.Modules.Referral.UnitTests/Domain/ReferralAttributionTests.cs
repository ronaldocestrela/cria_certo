using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Domain;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Domain.Errors;
using FluentAssertions;
using Xunit;

namespace CriaCerto.Modules.Referral.UnitTests.Domain;

public class ReferralAttributionTests
{
    [Fact]
    public void Create_WithValidInputsAndAntiFraudMetadata_ShouldBePending()
    {
        // Arrange
        var referralCodeId = Guid.NewGuid();
        var referrerParticipantId = Guid.NewGuid();
        var referredTenantId = Guid.NewGuid();

        // Act
        var result = ReferralAttribution.Create(
            referralCodeId,
            referrerParticipantId,
            referredTenantId,
            originIpAddress: "189.40.10.25",
            userAgent: "Mozilla/5.0 Chrome/120.0",
            deviceFingerprintHash: "hash-sha256-fingerprint");

        // Assert
        result.IsSuccess.Should().BeTrue();
        var attribution = result.Value;
        attribution.ReferralCodeId.Should().Be(referralCodeId);
        attribution.ReferrerParticipantId.Should().Be(referrerParticipantId);
        attribution.ReferredTenantId.Should().Be(referredTenantId);
        attribution.Status.Should().Be(AttributionStatus.Pending);
        attribution.ConvertedAtUtc.Should().BeNull();
        attribution.OriginIpAddress.Should().Be("189.40.10.25");
        attribution.UserAgent.Should().Be("Mozilla/5.0 Chrome/120.0");
        attribution.DeviceFingerprintHash.Should().Be("hash-sha256-fingerprint");
    }

    [Fact]
    public void MarkConverted_WhenPlanPaymentConfirmed_ShouldChangeStatusToConverted()
    {
        // Arrange
        var attribution = ReferralAttribution.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid()).Value;

        var paymentDate = DateTime.UtcNow;

        // Act
        var result = attribution.MarkConverted(paymentDate);

        // Assert
        result.IsSuccess.Should().BeTrue();
        attribution.Status.Should().Be(AttributionStatus.Converted);
        attribution.ConvertedAtUtc.Should().Be(paymentDate);
    }

    [Fact]
    public void MarkConverted_WhenAlreadyConverted_ShouldFail()
    {
        // Arrange
        var attribution = ReferralAttribution.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid()).Value;
        attribution.MarkConverted(DateTime.UtcNow);

        // Act
        var result = attribution.MarkConverted(DateTime.UtcNow);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ReferralAttribution.AlreadyConverted");
    }

    [Fact]
    public void FlagAsFraud_ShouldSetFraudulentStatusAndReason()
    {
        // Arrange
        var attribution = ReferralAttribution.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid()).Value;

        // Act
        var result = attribution.FlagAsFraud("Mesmo titular e cartão de crédito detectado");

        // Assert
        result.IsSuccess.Should().BeTrue();
        attribution.Status.Should().Be(AttributionStatus.Fraudulent);
        attribution.FraudReason.Should().Be("Mesmo titular e cartão de crédito detectado");
    }

    [Fact]
    public void MarkChurned_WhenConverted_ShouldChangeStatusToChurned()
    {
        // Arrange
        var attribution = ReferralAttribution.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid()).Value;
        attribution.MarkConverted(DateTime.UtcNow);

        // Act
        var result = attribution.MarkChurned();

        // Assert
        result.IsSuccess.Should().BeTrue();
        attribution.Status.Should().Be(AttributionStatus.Churned);
    }
}
