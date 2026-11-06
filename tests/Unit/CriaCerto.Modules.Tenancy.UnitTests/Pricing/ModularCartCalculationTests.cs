using CriaCerto.Modules.Tenancy.Application.Features.GetSubscriptionPlans;
using FluentAssertions;
using Xunit;

namespace CriaCerto.Modules.Tenancy.UnitTests.Pricing;

public class ModularCartCalculationTests
{
    private readonly ModularSubscriptionCatalogDto _catalog = GetSubscriptionPlansQueryHandler.GetDefaultModularCatalog();

    [Theory]
    [InlineData("TIER_500", 79.00, 63.20)]
    [InlineData("TIER_1500", 139.00, 111.20)]
    [InlineData("TIER_3000", 219.00, 175.20)]
    [InlineData("TIER_5000_PLUS", 349.00, 279.20)]
    public void Calculate_WhenOnlyCoreSelected_ShouldCalculateCowsPriceCorrectly(
        string tierCode, decimal expectedMonthly, decimal expectedAnnualMonthly)
    {
        // Act - Mensal
        var monthlyCart = ModularCartCalculator.Calculate(_catalog, tierCode, ["Cows"], isAnnual: false);
        // Act - Anual
        var annualCart = ModularCartCalculator.Calculate(_catalog, tierCode, ["Cows"], isAnnual: true);

        // Assert
        monthlyCart.TotalMonthlyEquivalent.Should().Be(expectedMonthly);
        monthlyCart.Items.Should().HaveCount(1);
        monthlyCart.Items[0].ModuleKey.Should().Be("Cows");

        annualCart.TotalMonthlyEquivalent.Should().Be(expectedAnnualMonthly);
        annualCart.TotalAnnualBilledAmount.Should().Be(expectedAnnualMonthly * 12m);
    }

    [Fact]
    public void Calculate_WhenCowsIsOmitted_ShouldCompulsorilyIncludeCows()
    {
        // Arrange - produtor selecionou apenas reprodução e sanitário sem passar Cows
        var selected = new[] { "Breeding", "Sanitary" };

        // Act
        var cart = ModularCartCalculator.Calculate(_catalog, "TIER_500", selected, isAnnual: false);

        // Assert
        // TIER_500: Cows (79) + Breeding (49) + Sanitary (49) = 177.00m
        cart.TotalMonthlyEquivalent.Should().Be(177.00m);
        cart.Items.Select(i => i.ModuleKey).Should().Contain("Cows");
        cart.Items.Select(i => i.ModuleKey).Should().Contain("Breeding");
        cart.Items.Select(i => i.ModuleKey).Should().Contain("Sanitary");
        cart.Items.Should().HaveCount(3);
    }

    [Fact]
    public void Calculate_WhenAllCommercialModulesSelected_ShouldSumAllPrices()
    {
        // Arrange
        // TIER_1500:
        // Cows (139) + Breeding (89) + Calving (69) + Growth (99) + Sanitary (89) + Nutrition (149) + Analytics (79) = 713.00m
        var allModules = new[] { "Cows", "Breeding", "Calving", "Growth", "Sanitary", "Nutrition", "Analytics" };

        // Act
        var cart = ModularCartCalculator.Calculate(_catalog, "TIER_1500", allModules, isAnnual: false);

        // Assert
        cart.TotalMonthlyEquivalent.Should().Be(713.00m);
        cart.Items.Should().HaveCount(7);
    }

    [Theory]
    [InlineData(500, 500, 0.35)]
    [InlineData(1000, 1500, 0.32)]
    [InlineData(0, 1500, 0.21)]
    public void Calculate_ShouldComputeCostPerHeadPerMonthAccurately(
        int activeHerdCount, int tierMaxHeads, decimal expectedCostPerHead)
    {
        // Arrange - Seleciona módulos que somem exatamente o valor
        var cart = ModularCartCalculator.Calculate(
            _catalog,
            tierMaxHeads == 500 ? "TIER_500" : "TIER_1500",
            tierMaxHeads == 500 ? ["Cows", "Breeding", "Sanitary"] : ["Cows", "Breeding", "Sanitary"],
            isAnnual: false,
            activeHerdCount: activeHerdCount);

        // Assert
        cart.CostPerHeadPerMonth.Should().Be(expectedCostPerHead);
    }

    [Fact]
    public void Calculate_WhenActiveHerdExceedsTierCapacity_ShouldMarkTierDisabled()
    {
        // 600 cabeças ativas excedem TIER_500 (max 500)
        var disabledCart = ModularCartCalculator.Calculate(_catalog, "TIER_500", ["Cows"], isAnnual: false, activeHerdCount: 600);
        disabledCart.IsTierDisabled.Should().BeTrue();

        // 600 cabeças ativas cabem na TIER_1500 (max 1500)
        var enabledCart = ModularCartCalculator.Calculate(_catalog, "TIER_1500", ["Cows"], isAnnual: false, activeHerdCount: 600);
        enabledCart.IsTierDisabled.Should().BeFalse();
    }
}
