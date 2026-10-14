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
}
