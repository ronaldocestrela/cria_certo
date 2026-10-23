using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Domain;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Domain.Errors;
using FluentAssertions;
using Xunit;

namespace CriaCerto.Modules.Referral.UnitTests.Domain;

public class PlanReferralDiscountPolicyTests
{
    [Fact]
    public void Create_PercentagePolicy_ShouldSucceed()
    {
        // Arrange
        var planCatalogId = Guid.NewGuid();

        // Act
        var result = PlanReferralDiscountPolicy.Create(
            planCatalogId,
            planCode: "PLAN_PRO",
            discountType: DiscountType.Percentage,
            discountValue: 15m,
            maxDiscountCapPercentage: 100m);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var policy = result.Value;
        policy.PlanCatalogId.Should().Be(planCatalogId);
        policy.PlanCode.Should().Be("PLAN_PRO");
        policy.DiscountType.Should().Be(DiscountType.Percentage);
        policy.DiscountValue.Should().Be(15m);
        policy.MaxDiscountCapPercentage.Should().Be(100m);
        policy.IsActive.Should().BeTrue();
    }

    [Fact]
    public void CalculateDiscount_Percentage_ShouldCalculateCorrectly()
    {
        // Arrange
        var policy = PlanReferralDiscountPolicy.Create(
            Guid.NewGuid(),
            "PLAN_ENTERPRISE",
            DiscountType.Percentage,
            discountValue: 20m,
            maxDiscountCapPercentage: 100m).Value;

        // Act
        var discount = policy.CalculateDiscount(basePrice: 500m);

        // Assert
        discount.Should().Be(100m); // 20% de 500 = 100
    }

    [Fact]
    public void CalculateDiscount_FixedValue_ShouldNotExceedCap()
    {
        // Arrange
        var policy = PlanReferralDiscountPolicy.Create(
            Guid.NewGuid(),
            "PLAN_STARTER",
            DiscountType.FixedValue,
            discountValue: 250m,
            maxDiscountCapPercentage: 50m).Value;

        // Act: Preço base 300, cap 50% = 150. O desconto é 250, mas deve limitar a 150.
        var discount = policy.CalculateDiscount(basePrice: 300m);

        // Assert
        discount.Should().Be(150m);
    }

    [Fact]
    public void UpdatePolicy_ValidValues_ShouldUpdatePropertiesAndSucceed()
    {
        // Arrange
        var policy = PlanReferralDiscountPolicy.Create(
            Guid.NewGuid(),
            "PLAN_PRO",
            DiscountType.Percentage,
            discountValue: 10m,
            maxDiscountCapPercentage: 100m).Value;

        // Act
        var result = policy.UpdatePolicy(DiscountType.FixedValue, 75m, 80m);

        // Assert
        result.IsSuccess.Should().BeTrue();
        policy.DiscountType.Should().Be(DiscountType.FixedValue);
        policy.DiscountValue.Should().Be(75m);
        policy.MaxDiscountCapPercentage.Should().Be(80m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void UpdatePolicy_WithZeroOrNegativeDiscountValue_ShouldFail(decimal invalidValue)
    {
        // Arrange
        var policy = PlanReferralDiscountPolicy.Create(
            Guid.NewGuid(),
            "PLAN_PRO",
            DiscountType.Percentage,
            discountValue: 10m,
            maxDiscountCapPercentage: 100m).Value;

        // Act
        var result = policy.UpdatePolicy(DiscountType.Percentage, invalidValue, 100m);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ReferralErrors.Policy.InvalidDiscountValue.Code);
    }

    [Fact]
    public void UpdatePolicy_WithPercentageGreaterThan100_ShouldFail()
    {
        // Arrange
        var policy = PlanReferralDiscountPolicy.Create(
            Guid.NewGuid(),
            "PLAN_PRO",
            DiscountType.Percentage,
            discountValue: 10m,
            maxDiscountCapPercentage: 100m).Value;

        // Act
        var result = policy.UpdatePolicy(DiscountType.Percentage, 105m, 100m);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ReferralErrors.Policy.InvalidPercentageDiscountValue.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(101)]
    public void UpdatePolicy_WithInvalidCap_ShouldFail(decimal invalidCap)
    {
        // Arrange
        var policy = PlanReferralDiscountPolicy.Create(
            Guid.NewGuid(),
            "PLAN_PRO",
            DiscountType.Percentage,
            discountValue: 10m,
            maxDiscountCapPercentage: 100m).Value;

        // Act
        var result = policy.UpdatePolicy(DiscountType.Percentage, 20m, invalidCap);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ReferralErrors.Policy.InvalidDiscountCap.Code);
    }

    [Fact]
    public void DeactivateAndReactivate_ShouldToggleIsActive()
    {
        // Arrange
        var policy = PlanReferralDiscountPolicy.Create(
            Guid.NewGuid(),
            "PLAN_PRO",
            DiscountType.Percentage,
            discountValue: 10m).Value;

        // Act & Assert
        policy.Deactivate();
        policy.IsActive.Should().BeFalse();

        policy.Reactivate();
        policy.IsActive.Should().BeTrue();
    }

    [Fact]
    public void CalculateDiscount_WhenFixedValueExceedsBasePrice_ShouldClampTo100PercentOfBasePrice()
    {
        // Arrange: Plano custa R$ 150, mas desconto fixo é R$ 50 e cliente tem 5 amigos ativos (R$ 250)
        var policy = PlanReferralDiscountPolicy.Create(
            Guid.NewGuid(), "STARTER", DiscountType.FixedValue, 50m, maxDiscountCapPercentage: 100m).Value;

        // Act
        var discount = policy.CalculateDiscount(basePrice: 150m, activeReferralsCount: 5);

        // Assert: Não pode passar de R$ 150 (teto de 100% da assinatura, sem saldo negativo)
        discount.Should().Be(150m);
    }

    [Fact]
    public void CalculateDiscount_WhenPercentageExceedsCap_ShouldClampStrictlyTo100PercentOrConfiguredCap()
    {
        // Arrange: 10% por amigo, mas com 12 amigos (120%) e cap configurado de 100%
        var policy = PlanReferralDiscountPolicy.Create(
            Guid.NewGuid(), "STARTER", DiscountType.Percentage, 10m, maxDiscountCapPercentage: 100m).Value;

        // Act
        var discount = policy.CalculateDiscount(basePrice: 200m, activeReferralsCount: 12);

        // Assert: 100% de R$ 200 = R$ 200
        discount.Should().Be(200m);
    }

    [Fact]
    public void CalculateDiscount_WhenConfiguredCapIsLowerThan100_ShouldRespectLowerCap()
    {
        // Arrange: 15% por amigo, cap de 50%, 4 amigos (60%)
        var policy = PlanReferralDiscountPolicy.Create(
            Guid.NewGuid(), "STARTER", DiscountType.Percentage, 15m, maxDiscountCapPercentage: 50m).Value;

        // Act
        var discount = policy.CalculateDiscount(basePrice: 300m, activeReferralsCount: 4);

        // Assert: Cap de 50% de R$ 300 = R$ 150
        discount.Should().Be(150m);
    }
}
