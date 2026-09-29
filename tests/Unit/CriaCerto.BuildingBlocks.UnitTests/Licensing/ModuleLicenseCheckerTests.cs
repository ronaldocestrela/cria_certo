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
    [InlineData("Pro Fazenda", "Nutrition", true)]
    [InlineData("Pro Fazenda", "Breeding", true)]
    [InlineData("Pro Fazenda", "Calving", true)]
    [InlineData("Pro Fazenda", "Growth", true)]
    [InlineData("Pro Fazenda", "Cows", true)]
    [InlineData("Pro Fazenda", "cows", true)]
    [InlineData("Pro Fazenda", "Modules.Cows", true)]
    [InlineData("Pro Fazenda", "Sanitary", false)]
    [InlineData("Starter Pecuária", "Breeding", true)]
    [InlineData("Starter Pecuária", "Calving", true)]
    [InlineData("Starter Pecuária", "Cows", true)]
    [InlineData("Starter Pecuária", "cows", true)]
    [InlineData("Starter Pecuária", "Modules.Cows", true)]
    [InlineData("Starter Pecuária", "Nutrition", false)]
    [InlineData("Starter Pecuária", "Feedlot", false)]
    [InlineData("Enterprise Confinamento", "Sanitary", true)]
    [InlineData("Enterprise Confinamento", "Feedlot", true)]
    [InlineData("Enterprise Confinamento", "Cows", true)]
    [InlineData("Enterprise Confinamento", "QualquerModuloNovo", true)]
    public void HasAccess_WithCommercialNames_ShouldEvaluatePermissionsCorrectly(string plan, string module, bool expected)
    {
        var result = ModuleLicenseChecker.HasAccess(plan, module);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("v2.0 - Nova Revisão", "Cows", true)]
    [InlineData("v2.0 - Nova Revisão", "cows", true)]
    [InlineData("v2.0 - Nova Revisão", "Breeding", true)]
    [InlineData("v2.0 - Nova Revisão", "Calving", true)]
    [InlineData("v2.0 - Nova Revisão", "Feedlot", false)]
    public void HasAccess_WithUnrecognizedVersionString_ShouldSafelyFallbackToStarterPlan(string plan, string module, bool expected)
    {
        var result = ModuleLicenseChecker.HasAccess(plan, module);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("Modules.Cows", "Cows")]
    [InlineData("cows", "Cows")]
    [InlineData("cattle", "Cows")]
    [InlineData("plantel", "Cows")]
    [InlineData("Modules.Breeding", "Breeding")]
    [InlineData("Feedlot", "Feedlot")]
    public void NormalizeModule_ShouldNormalizeKeysCorrectly(string input, string expected)
    {
        var result = ModuleLicenseChecker.NormalizeModule(input);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("Pro", null)]
    [InlineData("Pro", "")]
    [InlineData("Pro", "   ")]
    public void HasAccess_WithNullOrWhitespaceModule_ShouldReturnFalse(string plan, string? module)
    {
        var result = ModuleLicenseChecker.HasAccess(plan, module);

        result.Should().BeFalse();
    }
}
