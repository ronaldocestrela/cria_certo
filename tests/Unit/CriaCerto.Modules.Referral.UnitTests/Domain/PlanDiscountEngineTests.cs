using CriaCerto.Modules.Referral.Application.Domain;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace CriaCerto.Modules.Referral.UnitTests.Domain;

/// <summary>
/// Roadmap 6.2 Testes Automatizados: Motor de cálculo de desconto por plano de cliente.
/// </summary>
public class PlanDiscountEngineTests
{
    [Theory]
    [InlineData(100, 1, 10, 10)]      // 10% de R$ 100 com 1 amigo = R$ 10
    [InlineData(250, 2, 10, 50)]      // 10% * 2 = 20% de R$ 250 = R$ 50
    [InlineData(500, 3, 15, 225)]     // 15% * 3 = 45% de R$ 500 = R$ 225
    [InlineData(1000, 5, 20, 1000)]   // 20% * 5 = 100% de R$ 1000 = R$ 1000
    public void CalculateDiscount_Percentage_WithVariousReferrals_ShouldCalculateExpectedDiscount(
        decimal basePrice, int activeReferrals, decimal discountPercentage, decimal expectedDiscount)
    {
        // Arrange
        var policy = PlanReferralDiscountPolicy.Create(
            Guid.NewGuid(), "PRO", DiscountType.Percentage, discountPercentage, maxDiscountCapPercentage: 100m).Value;

        // Act
        var discount = policy.CalculateDiscount(basePrice, activeReferrals);

        // Assert
        discount.Should().Be(expectedDiscount);
    }

    [Theory]
    [InlineData(200, 1, 50, 50)]      // R$ 50 fixo por amigo, 1 amigo = R$ 50
    [InlineData(300, 3, 50, 150)]     // R$ 50 * 3 = R$ 150
    [InlineData(400, 4, 100, 400)]    // R$ 100 * 4 = R$ 400 (100% do plano)
    public void CalculateDiscount_FixedValue_WithVariousReferrals_ShouldCalculateExpectedDiscount(
        decimal basePrice, int activeReferrals, decimal fixedDiscount, decimal expectedDiscount)
    {
        // Arrange
        var policy = PlanReferralDiscountPolicy.Create(
            Guid.NewGuid(), "STARTER", DiscountType.FixedValue, fixedDiscount, maxDiscountCapPercentage: 100m).Value;

        // Act
        var discount = policy.CalculateDiscount(basePrice, activeReferrals);

        // Assert
        discount.Should().Be(expectedDiscount);
    }

    [Fact]
    public void CalculateDiscount_Percentage_ExceedingCustomCap_ShouldClampToCap()
    {
        // Arrange: Cap de 50%, desconto de 20% por amigo, 4 amigos indicados (80% bruto)
        var policy = PlanReferralDiscountPolicy.Create(
            Guid.NewGuid(), "PRO", DiscountType.Percentage, 20m, maxDiscountCapPercentage: 50m).Value;

        // Act: Plano R$ 400. 80% seria R$ 320, mas cap de 50% limita a R$ 200.
        var discount = policy.CalculateDiscount(basePrice: 400m, activeReferralsCount: 4);

        // Assert
        discount.Should().Be(200m);
    }

    [Fact]
    public void CalculateDiscount_FixedValue_ExceedingBasePrice_ShouldStrictlyClampTo100Percent()
    {
        // Arrange: Plano de R$ 150. Desconto de R$ 80 por amigo, com 3 amigos (R$ 240 bruto).
        var policy = PlanReferralDiscountPolicy.Create(
            Guid.NewGuid(), "STARTER", DiscountType.FixedValue, 80m, maxDiscountCapPercentage: 100m).Value;

        // Act
        var discount = policy.CalculateDiscount(basePrice: 150m, activeReferralsCount: 3);

        // Assert: Não pode ultrapassar R$ 150 (nunca gera saldo negativo ou devolução)
        discount.Should().Be(150m);
    }

    [Fact]
    public void CalculateDiscount_WhenPolicyIsInactive_ShouldReturnZero()
    {
        // Arrange
        var policy = PlanReferralDiscountPolicy.Create(
            Guid.NewGuid(), "STARTER", DiscountType.Percentage, 25m, 100m).Value;
        policy.Deactivate();

        // Act
        var discount = policy.CalculateDiscount(basePrice: 200m, activeReferralsCount: 2);

        // Assert
        discount.Should().Be(0m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public void CalculateDiscount_WhenBasePriceIsZeroOrNegative_ShouldReturnZero(decimal basePrice)
    {
        // Arrange
        var policy = PlanReferralDiscountPolicy.Create(
            Guid.NewGuid(), "STARTER", DiscountType.Percentage, 20m, 100m).Value;

        // Act
        var discount = policy.CalculateDiscount(basePrice, activeReferralsCount: 2);

        // Assert
        discount.Should().Be(0m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CalculateDiscount_WhenActiveReferralsCountIsZeroOrNegative_ShouldReturnZero(int activeReferrals)
    {
        // Arrange
        var policy = PlanReferralDiscountPolicy.Create(
            Guid.NewGuid(), "STARTER", DiscountType.FixedValue, 50m, 100m).Value;

        // Act
        var discount = policy.CalculateDiscount(basePrice: 300m, activeReferralsCount: activeReferrals);

        // Assert
        discount.Should().Be(0m);
    }
}
