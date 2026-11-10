using System;
using System.Collections.Generic;
using System.Linq;
using CriaCerto.Web.Client.Services;
using FluentAssertions;
using Xunit;

namespace CriaCerto.Web.Client.UnitTests.Components;

public class ModuleGuardUpsellTests
{
    private readonly ModularSubscriptionCatalogModel _catalog = TenancyApiClient.GetDefaultModularCatalog();

    [Theory]
    [InlineData("Nutrition", "TIER_500", 59.00, 500)]
    [InlineData("Nutrition", "TIER_1500", 89.00, 1500)]
    [InlineData("Nutrition", "TIER_3000", 129.00, 3000)]
    [InlineData("Nutrition", "TIER_5000_PLUS", 179.00, 10000)]
    [InlineData("Breeding", "TIER_500", 79.00, 500)]
    [InlineData("Breeding", "TIER_1500", 119.00, 1500)]
    [InlineData("Sanitary", "TIER_500", 49.00, 500)]
    [InlineData("Analytics", "TIER_500", 69.00, 500)]
    public void ResolveModuleUpsell_ShouldCalculateAccurateDynamicPriceAndTierCapacity(
        string moduleKey,
        string tierCode,
        decimal expectedMonthlyPrice,
        int expectedMaxHeads)
    {
        // Act
        var upsell = ModuleGuardUpsellCalculator.ResolveUpsellInfo(_catalog, moduleKey, tierCode);

        // Assert
        upsell.Should().NotBeNull();
        upsell.MonthlyPrice.Should().Be(expectedMonthlyPrice);
        upsell.MaxHeads.Should().Be(expectedMaxHeads);
        upsell.TierCode.Should().Be(tierCode);
        upsell.ModuleName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void FormatUpsellCallout_ShouldMatchRoadmapFormatExactly()
    {
        // Arrange & Act
        var upsell = ModuleGuardUpsellCalculator.ResolveUpsellInfo(_catalog, "Nutrition", "TIER_500");
        var callout = ModuleGuardUpsellCalculator.FormatUpsellCallout(upsell);

        // Assert
        // Format: "Adicione o Módulo de {Nome} por + R$ {Valor:N2}/mês (para sua faixa atual de até {X} cabeças)"
        callout.Should().Contain("Adicione o Módulo de Nutrição & Confinamento");
        callout.Should().Contain("+ R$ 59,00/mês");
        callout.Should().Contain("(para sua faixa atual de até 500 cabeças)");
    }

    [Fact]
    public void LegacyPlanNames_ShouldNotBeContainedInUpsellStrings()
    {
        // Arrange
        var testModules = new[] { "Nutrition", "Breeding", "Calving", "Growth", "Sanitary", "Analytics" };
        var tiers = new[] { "TIER_500", "TIER_1500", "TIER_3000", "TIER_5000_PLUS" };

        foreach (var mod in testModules)
        {
            foreach (var tier in tiers)
            {
                var upsell = ModuleGuardUpsellCalculator.ResolveUpsellInfo(_catalog, mod, tier);
                var callout = ModuleGuardUpsellCalculator.FormatUpsellCallout(upsell);

                // Assert
                callout.Should().NotContain("Starter");
                callout.Should().NotContain("Pro");
                callout.Should().NotContain("Enterprise");
                upsell.ModuleName.Should().NotContain("Starter");
                upsell.ModuleName.Should().NotContain("Pro");
                upsell.ModuleName.Should().NotContain("Enterprise");
            }
        }
    }

    [Fact]
    public void BuildUpdatedModuleKeys_ShouldAlwaysPreserveCoreCowsAndExistingModules()
    {
        // Arrange
        var currentModules = new List<string> { "Cows", "Breeding" };

        // Act
        var updated = ModuleGuardUpsellCalculator.BuildUpdatedModuleKeys(currentModules, "Nutrition");

        // Assert
        updated.Should().Contain("Cows");
        updated.Should().Contain("Breeding");
        updated.Should().Contain("Nutrition");
        updated.Should().HaveCount(3);
    }

    [Fact]
    public void BuildUpdatedModuleKeys_WhenCurrentModulesNull_ShouldIncludeCoreCowsAndNewModule()
    {
        // Act
        var updated = ModuleGuardUpsellCalculator.BuildUpdatedModuleKeys(null, "Sanitary");

        // Assert
        updated.Should().Contain("Cows");
        updated.Should().Contain("Sanitary");
        updated.Should().HaveCount(2);
    }

    [Theory]
    [InlineData("nutrição", "Nutrition")]
    [InlineData("feedlot", "Nutrition")]
    [InlineData("confinamento", "Nutrition")]
    [InlineData("iatf", "Breeding")]
    [InlineData("reprodução", "Breeding")]
    [InlineData("partos", "Calving")]
    [InlineData("maternidade", "Calving")]
    [InlineData("manejo", "Growth")]
    [InlineData("pesagem", "Growth")]
    [InlineData("sanidade", "Sanitary")]
    [InlineData("vacinas", "Sanitary")]
    [InlineData("analytics", "Analytics")]
    [InlineData("indicadores", "Analytics")]
    public void ResolveCommercialModuleKey_ShouldMapAliasesToCommercialCatalogKey(string alias, string expectedCommercialKey)
    {
        // Act
        var key = ModuleGuardUpsellCalculator.ResolveCommercialModuleKey(alias);

        // Assert
        key.Should().Be(expectedCommercialKey);
    }
}

