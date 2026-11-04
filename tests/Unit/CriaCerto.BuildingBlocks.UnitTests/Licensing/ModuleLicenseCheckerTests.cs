using System.Collections.Generic;
using System.Reflection;
using CriaCerto.BuildingBlocks.Abstractions.Licensing;
using FluentAssertions;
using Xunit;

namespace CriaCerto.BuildingBlocks.UnitTests.Licensing;

public class ModuleLicenseCheckerTests
{
    [Theory]
    [InlineData("Starter", "Starter")]
    [InlineData("Pro", "Pro")]
    [InlineData("Enterprise", "Enterprise")]
    public void NormalizePlan_WithCanonicalPlans_ShouldReturnCanonicalForm(string input, string expected)
    {
        var result = ModuleLicenseChecker.NormalizePlan(input);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("starter", "Starter")]
    [InlineData("STARTER", "Starter")]
    [InlineData("pro", "Pro")]
    [InlineData("PRO", "Pro")]
    [InlineData("enterprise", "Enterprise")]
    [InlineData("ENTERPRISE", "Enterprise")]
    public void NormalizePlan_WithMixedCase_ShouldReturnCanonicalForm(string input, string expected)
    {
        var result = ModuleLicenseChecker.NormalizePlan(input);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("Starter Pecuária", "Starter")]
    [InlineData("Starter Pecuaria", "Starter")]
    [InlineData("Plano Starter", "Starter")]
    [InlineData("Pro Fazenda", "Pro")]
    [InlineData("Plano Pro", "Pro")]
    [InlineData("Enterprise Confinamento", "Enterprise")]
    [InlineData("Plano Enterprise", "Enterprise")]
    public void NormalizePlan_WithCommercialAliases_ShouldReturnCanonicalForm(string input, string expected)
    {
        var result = ModuleLicenseChecker.NormalizePlan(input);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("Pro 2026.1", "Pro")]
    [InlineData("Enterprise 2026.1", "Enterprise")]
    [InlineData("Starter 2026.1", "Starter")]
    public void NormalizePlan_WithBackofficeReleaseVersions_ShouldReturnCanonicalForm(string input, string expected)
    {
        var result = ModuleLicenseChecker.NormalizePlan(input);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizePlan_WithNullOrWhitespace_ShouldDefaultToStarter(string? input)
    {
        var result = ModuleLicenseChecker.NormalizePlan(input);

        result.Should().Be("Starter");
    }

    [Theory]
    [InlineData("Modules.Cows", "Cows")]
    [InlineData("cows", "Cows")]
    [InlineData("cow", "Cows")]
    [InlineData("cattle", "Cows")]
    [InlineData("plantel", "Cows")]
    [InlineData("Modules.Breeding", "Breeding")]
    [InlineData("Modules.Tenancy", "Tenancy")]
    [InlineData("Feedlot", "Feedlot")]
    public void NormalizeModule_ShouldNormalizeKeysCorrectly(string input, string expected)
    {
        var result = ModuleLicenseChecker.NormalizeModule(input);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("Cows")]
    [InlineData("cows")]
    [InlineData("plantel")]
    [InlineData("cattle")]
    [InlineData("Modules.Cows")]
    [InlineData("Tenancy")]
    [InlineData("tenancy")]
    [InlineData("Modules.Tenancy")]
    public void HasModuleAccess_ForCoreModules_ShouldAlwaysReturnTrueEvenWhenSubscribedSetIsEmpty(string coreModule)
    {
        var emptyModules = Array.Empty<string>();

        ModuleLicenseChecker.HasModuleAccess(emptyModules, coreModule).Should().BeTrue();
        ModuleLicenseChecker.HasModuleAccess(null!, coreModule).Should().BeTrue();
    }

    [Fact]
    public void HasModuleAccess_WithArbitraryModuleCombination_ShouldAllowOnlySubscribedAndCoreModules()
    {
        var subscribedModules = new[] { "Cows", "Sanitary", "Nutrition" };

        ModuleLicenseChecker.HasModuleAccess(subscribedModules, "Cows").Should().BeTrue();
        ModuleLicenseChecker.HasModuleAccess(subscribedModules, "Tenancy").Should().BeTrue();
        ModuleLicenseChecker.HasModuleAccess(subscribedModules, "Sanitary").Should().BeTrue();
        ModuleLicenseChecker.HasModuleAccess(subscribedModules, "sanitary").Should().BeTrue();
        ModuleLicenseChecker.HasModuleAccess(subscribedModules, "Modules.Sanitary").Should().BeTrue();
        ModuleLicenseChecker.HasModuleAccess(subscribedModules, "Nutrition").Should().BeTrue();

        // Ter "Cows" (Plantel Base) NÃO concede acesso implícito a "Breeding" (IATF/DG)
        ModuleLicenseChecker.HasModuleAccess(subscribedModules, "Breeding").Should().BeFalse();
        ModuleLicenseChecker.HasModuleAccess(subscribedModules, "Calving").Should().BeFalse();
        ModuleLicenseChecker.HasModuleAccess(subscribedModules, "Growth").Should().BeFalse();
        ModuleLicenseChecker.HasModuleAccess(subscribedModules, "Analytics").Should().BeFalse();
    }

    [Fact]
    public void HasModuleAccess_WhenOnlyCowsSubscribed_ShouldBlockBreedingAndOtherAddons()
    {
        var subscribedModules = new[] { "Cows" };

        ModuleLicenseChecker.HasModuleAccess(subscribedModules, "Cows").Should().BeTrue();
        ModuleLicenseChecker.HasModuleAccess(subscribedModules, "Breeding").Should().BeFalse();
        ModuleLicenseChecker.HasModuleAccess(subscribedModules, "Calving").Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void HasModuleAccess_WithNullOrWhitespaceRequiredModule_ShouldReturnFalse(string? requiredModule)
    {
        var subscribedModules = new[] { "Cows", "Breeding", "Sanitary" };

        ModuleLicenseChecker.HasModuleAccess(subscribedModules, requiredModule!).Should().BeFalse();
    }

    [Fact]
    public void ModuleLicenseChecker_ShouldNotContainStaticPlanAccessDictionary()
    {
        var planAccessField = typeof(ModuleLicenseChecker).GetField(
            "PlanAccess",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

        planAccessField.Should().BeNull("o dicionário estático PlanAccess deve ser removido no modelo modular Self-Service");
    }
}
