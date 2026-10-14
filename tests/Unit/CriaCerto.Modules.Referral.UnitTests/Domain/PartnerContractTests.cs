using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Domain;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Domain.Errors;
using FluentAssertions;
using Xunit;

namespace CriaCerto.Modules.Referral.UnitTests.Domain;

public class PartnerContractTests
{
    [Fact]
    public void Create_PercentageRecurring_WithValidInputs_ShouldSucceed()
    {
        // Arrange
        var participantId = Guid.NewGuid();
        var validFrom = DateTime.UtcNow;

        // Act
        var result = PartnerContract.Create(
            participantId,
            CommissionType.PercentageRecurring,
            commissionValue: 20m,
            maxRecurringMonths: 12,
            paymentTermsDays: 30,
            pixKey: "12345678909",
            pixKeyType: PixKeyType.Cpf,
            legalDocumentNumber: "123.456.789-09",
            validFromUtc: validFrom);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var contract = result.Value;
        contract.ParticipantId.Should().Be(participantId);
        contract.CommissionType.Should().Be(CommissionType.PercentageRecurring);
        contract.CommissionValue.Should().Be(20m);
        contract.MaxRecurringMonths.Should().Be(12);
        contract.PaymentTermsDays.Should().Be(30);
        contract.PixKey.Should().Be("12345678909");
        contract.PixKeyType.Should().Be(PixKeyType.Cpf);
        contract.LegalDocumentNumber.Should().Be("123.456.789-09");
        contract.ValidFromUtc.Should().Be(validFrom);
        contract.ValidToUtc.Should().BeNull();
        contract.IsActive.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(101)]
    public void Create_PercentageCommission_WithInvalidRate_ShouldFail(decimal invalidRate)
    {
        // Act
        var result = PartnerContract.Create(
            Guid.NewGuid(),
            CommissionType.PercentageRecurring,
            commissionValue: invalidRate,
            maxRecurringMonths: 12,
            paymentTermsDays: 30,
            pixKey: "consultor@agrifirma.com.br",
            pixKeyType: PixKeyType.Email,
            legalDocumentNumber: "12.345.678/0001-90",
            validFromUtc: DateTime.UtcNow);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PartnerContract.InvalidPercentageRate");
    }

    [Fact]
    public void Create_FixedPerConversion_WithZeroOrNegative_ShouldFail()
    {
        // Act
        var result = PartnerContract.Create(
            Guid.NewGuid(),
            CommissionType.FixedPerConversion,
            commissionValue: 0m,
            maxRecurringMonths: null,
            paymentTermsDays: 30,
            pixKey: "consultor@agrifirma.com.br",
            pixKeyType: PixKeyType.Email,
            legalDocumentNumber: "12.345.678/0001-90",
            validFromUtc: DateTime.UtcNow);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PartnerContract.InvalidFixedAmount");
    }

    [Fact]
    public void CloseContract_WithFutureDate_ShouldSetValidToAndDeactivate()
    {
        // Arrange
        var contract = PartnerContract.Create(
            Guid.NewGuid(),
            CommissionType.PercentageFirstMonth,
            commissionValue: 30m,
            maxRecurringMonths: 1,
            paymentTermsDays: 30,
            pixKey: "12345678909",
            pixKeyType: PixKeyType.Cpf,
            legalDocumentNumber: "123.456.789-09",
            validFromUtc: DateTime.UtcNow.AddDays(-10)).Value;

        var closeDate = DateTime.UtcNow;

        // Act
        var result = contract.CloseContract(closeDate);

        // Assert
        result.IsSuccess.Should().BeTrue();
        contract.ValidToUtc.Should().Be(closeDate);
        contract.IsActive.Should().BeFalse();
    }
}
