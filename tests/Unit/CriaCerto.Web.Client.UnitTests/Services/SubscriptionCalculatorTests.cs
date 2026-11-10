using CriaCerto.Web.Client.Services;
using FluentAssertions;
using Xunit;

namespace CriaCerto.Web.Client.UnitTests.Services;

public static class ModularSubscriptionCalculator
{
    public static decimal CalculateTotalPrice(
        ModularSubscriptionCatalogModel catalog,
        string tierCode,
        IEnumerable<string> selectedModuleKeys,
        bool isAnnual)
    {
        var keys = new HashSet<string>(selectedModuleKeys, StringComparer.OrdinalIgnoreCase);
        // Cows is always required
        keys.Add("Cows");

        decimal total = 0m;
        foreach (var mod in catalog.Modules.Where(m => keys.Contains(m.ModuleKey)))
        {
            var priceOption = mod.PricesByTier.FirstOrDefault(p => string.Equals(p.TierCode, tierCode, StringComparison.OrdinalIgnoreCase))
                ?? mod.PricesByTier.FirstOrDefault();

            if (priceOption != null)
            {
                total += isAnnual ? priceOption.AnnualPriceMonthly : priceOption.MonthlyPrice;
            }
        }

        return total;
    }

    public static decimal CalculateCostPerHeadPerMonth(decimal totalPrice, int activeHerdCount, int tierMaxHeads)
    {
        var denominator = activeHerdCount > 0 ? activeHerdCount : Math.Max(1, tierMaxHeads);
        return Math.Round(totalPrice / denominator, 2);
    }

    public static bool IsTierDisabled(int tierMaxHeads, int activeHerdCount)
    {
        return activeHerdCount > 0 && tierMaxHeads < activeHerdCount;
    }

    public static HashSet<string> EnsureCoreModuleIncluded(IEnumerable<string>? modules)
    {
        var result = modules != null
            ? new HashSet<string>(modules, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        result.Add("Cows");
        return result;
    }

    public static HashSet<string> ResolveLandingPreset(string preset, ModularSubscriptionCatalogModel catalog)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Cows" };
        switch (preset.ToLowerInvariant())
        {
            case "plantel":
                break;
            case "cria":
                result.Add("Breeding");
                result.Add("Calving");
                break;
            case "engorda":
                result.Add("Growth");
                result.Add("Nutrition");
                result.Add("Sanitary");
                break;
            case "completo":
                foreach (var mod in catalog.Modules)
                {
                    result.Add(mod.ModuleKey);
                }
                break;
        }
        return result;
    }
}

public class SubscriptionCalculatorTests
{
    private readonly ModularSubscriptionCatalogModel _catalog = TenancyApiClient.GetDefaultModularCatalog();

    [Fact]
    public void CalculateTotalPrice_WhenOnlyCoreSelected_ShouldReturnCowsPriceForTier()
    {
        // Act
        var price500Monthly = ModularSubscriptionCalculator.CalculateTotalPrice(_catalog, "TIER_500", new[] { "Cows" }, isAnnual: false);
        var price500Annual = ModularSubscriptionCalculator.CalculateTotalPrice(_catalog, "TIER_500", new[] { "Cows" }, isAnnual: true);
        var price1500Monthly = ModularSubscriptionCalculator.CalculateTotalPrice(_catalog, "TIER_1500", new[] { "Cows" }, isAnnual: false);

        // Assert
        price500Monthly.Should().Be(149.00m);
        price500Annual.Should().Be(126.65m);
        price1500Monthly.Should().Be(229.00m);
    }

    [Fact]
    public void CalculateTotalPrice_WhenMultipleModulesSelected_ShouldSumPricesCorrectly()
    {
        // Arrange
        // TIER_1500: Cows (229.00), Breeding (119.00), Sanitary (69.00) = 417.00m
        var selected = new[] { "Cows", "Breeding", "Sanitary" };

        // Act
        var total = ModularSubscriptionCalculator.CalculateTotalPrice(_catalog, "TIER_1500", selected, isAnnual: false);

        // Assert
        total.Should().Be(417.00m);
    }

    [Fact]
    public void CalculateTotalPrice_WhenCowsOmittedInInput_ShouldAutomaticallyIncludeCows()
    {
        // Arrange - user only passed Breeding and Sanitary
        var selected = new[] { "Breeding", "Sanitary" };

        // Act
        var total = ModularSubscriptionCalculator.CalculateTotalPrice(_catalog, "TIER_500", selected, isAnnual: false);

        // Assert: Cows (149.00) + Breeding (79.00) + Sanitary (49.00) = 277.00m
        total.Should().Be(277.00m);
    }

    [Theory]
    [InlineData(417.00, 412, 1500, 1.01)]
    [InlineData(149.00, 500, 500, 0.30)]
    [InlineData(149.00, 0, 500, 0.30)]
    public void CalculateCostPerHeadPerMonth_ShouldCalculateCorrectly(
        double totalPrice, int activeHerdCount, int tierMaxHeads, double expectedCostPerHead)
    {
        // Act
        var cost = ModularSubscriptionCalculator.CalculateCostPerHeadPerMonth((decimal)totalPrice, activeHerdCount, tierMaxHeads);

        // Assert
        cost.Should().Be((decimal)expectedCostPerHead);
    }

    [Fact]
    public void IsTierDisabled_WhenActiveHerdExceedsTierCapacity_ShouldReturnTrue()
    {
        // 650 active cows on TIER_500 (max 500)
        ModularSubscriptionCalculator.IsTierDisabled(500, 650).Should().BeTrue();

        // 650 active cows on TIER_1500 (max 1500)
        ModularSubscriptionCalculator.IsTierDisabled(1500, 650).Should().BeFalse();

        // 0 active cows (new farm) on TIER_500
        ModularSubscriptionCalculator.IsTierDisabled(500, 0).Should().BeFalse();
    }

    [Fact]
    public void EnsureCoreModuleIncluded_ShouldAlwaysContainCowsCaseInsensitive()
    {
        var result = ModularSubscriptionCalculator.EnsureCoreModuleIncluded(new[] { "breeding", "growth" });

        result.Should().Contain("Cows");
        result.Should().Contain("breeding");
        result.Should().Contain("growth");
    }

    [Theory]
    [InlineData("plantel", 1)]
    [InlineData("cria", 3)]
    [InlineData("engorda", 4)]
    [InlineData("completo", 7)]
    public void ResolveLandingPreset_ShouldReturnExpectedModuleCounts(string preset, int expectedCount)
    {
        var modules = ModularSubscriptionCalculator.ResolveLandingPreset(preset, _catalog);

        modules.Should().Contain("Cows");
        modules.Count.Should().Be(expectedCount);
    }

    [Fact]
    public void CalculateTotalPrice_WhenPresetCriaSelected_ShouldSumCorrectlyInMonthlyAndAnnual()
    {
        // Preset "cria": Cows + Breeding + Calving
        var modules = ModularSubscriptionCalculator.ResolveLandingPreset("cria", _catalog);

        // TIER_500: Cows (149.00 / 126.65) + Breeding (79.00 / 67.15) + Calving (49.00 / 41.65)
        var monthly = ModularSubscriptionCalculator.CalculateTotalPrice(_catalog, "TIER_500", modules, isAnnual: false);
        var annual = ModularSubscriptionCalculator.CalculateTotalPrice(_catalog, "TIER_500", modules, isAnnual: true);

        monthly.Should().Be(277.00m);
        annual.Should().Be(235.45m);
    }

    [Fact]
    public void CalculateTotalPrice_WhenPresetCompletoSelected_ShouldSumAllSevenModules()
    {
        var modules = ModularSubscriptionCalculator.ResolveLandingPreset("completo", _catalog);
        modules.Count.Should().Be(7);

        // TIER_3000: Cows(349) + Breeding(169) + Calving(119) + Growth(149) + Sanitary(99) + Nutrition(129) + Analytics(149) = 1163.00m
        var monthly = ModularSubscriptionCalculator.CalculateTotalPrice(_catalog, "TIER_3000", modules, isAnnual: false);
        monthly.Should().Be(1163.00m);
    }

    [Fact]
    public void CalculateCostPerHeadPerMonth_OnLandingPage_ShouldUseTierMaxHeadsWhenNoHerdProvided()
    {
        // Simulation on landing page uses activeHerdCount = 0 -> denominator is tierMaxHeads
        // TIER_500: Total = 277.00m, maxHeads = 500 -> 277 / 500 = 0.55
        var costPerHead = ModularSubscriptionCalculator.CalculateCostPerHeadPerMonth(277.00m, activeHerdCount: 0, tierMaxHeads: 500);
        costPerHead.Should().Be(0.55m);

        // TIER_1500: Total = 417.00m, maxHeads = 1500 -> 417 / 1500 = 0.28
        var costPerHead1500 = ModularSubscriptionCalculator.CalculateCostPerHeadPerMonth(417.00m, activeHerdCount: 0, tierMaxHeads: 1500);
        costPerHead1500.Should().Be(0.28m);
    }
}

