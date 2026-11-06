using CriaCerto.Modules.Tenancy.Application.Domain;
using FluentAssertions;
using Xunit;

namespace CriaCerto.Modules.Tenancy.UnitTests.Domain;

public class HeadTierCapacityValidationTests
{
    [Theory]
    [InlineData(HeadTierCapacityLimits.Tier500, 1, true)]
    [InlineData(HeadTierCapacityLimits.Tier500, 500, true)]
    [InlineData(HeadTierCapacityLimits.Tier500, 501, false)]
    [InlineData(HeadTierCapacityLimits.Tier1500, 501, true)]
    [InlineData(HeadTierCapacityLimits.Tier1500, 1500, true)]
    [InlineData(HeadTierCapacityLimits.Tier1500, 1501, false)]
    [InlineData(HeadTierCapacityLimits.Tier3000, 1501, true)]
    [InlineData(HeadTierCapacityLimits.Tier3000, 3000, true)]
    [InlineData(HeadTierCapacityLimits.Tier3000, 3001, false)]
    [InlineData(HeadTierCapacityLimits.Tier5000Plus, 3001, true)]
    [InlineData(HeadTierCapacityLimits.Tier5000Plus, 10000, true)]
    [InlineData(HeadTierCapacityLimits.Tier5000Plus, 10001, false)]
    public void IsCapacityWithinTier_ShouldValidateStrictlyAgainstTierMaxHeads(string tierCode, int capacity, bool expected)
    {
        var isValid = HeadTierCapacityLimits.IsCapacityWithinTier(tierCode, capacity);

        isValid.Should().Be(expected);
    }

    [Theory]
    [InlineData(HeadTierCapacityLimits.Tier500, -1)]
    [InlineData(HeadTierCapacityLimits.Tier500, 0)]
    [InlineData(null, 500)]
    [InlineData("", 500)]
    [InlineData("INVALID_TIER", 500)]
    public void IsCapacityWithinTier_WithInvalidCapacityOrTier_ShouldReturnFalse(string? tierCode, int capacity)
    {
        var isValid = HeadTierCapacityLimits.IsCapacityWithinTier(tierCode, capacity);

        isValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("TIER_500", 500)]
    [InlineData("tier_500", 500)]
    [InlineData(" TIER_1500 ", 1500)]
    [InlineData("TIER_3000", 3000)]
    [InlineData("TIER_5000_PLUS", 10000)]
    [InlineData("tier_5000_plus", 10000)]
    [InlineData(null, 500)]
    [InlineData("UNKNOWN", 500)]
    public void GetMaxHeadsForTier_ShouldReturnCorrectLimit(string? tierCode, int expectedMaxHeads)
    {
        var maxHeads = HeadTierCapacityLimits.GetMaxHeadsForTier(tierCode);

        maxHeads.Should().Be(expectedMaxHeads);
    }

    [Theory]
    [InlineData("TIER_500", true)]
    [InlineData("tier_500", true)]
    [InlineData("TIER_1500", true)]
    [InlineData("TIER_3000", true)]
    [InlineData("TIER_5000_PLUS", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("STARTER", false)]
    [InlineData("PRO", false)]
    public void IsValidHeadTierCode_ShouldValidateKnownTiers(string? tierCode, bool expected)
    {
        var isValid = HeadTierCapacityLimits.IsValidHeadTierCode(tierCode);

        isValid.Should().Be(expected);
    }

    [Fact]
    public void GetTierDisplayName_ShouldReturnUserFriendlyPecuarioLabels()
    {
        HeadTierCapacityLimits.GetTierDisplayName("TIER_500").Should().Be("Até 500 cabeças");
        HeadTierCapacityLimits.GetTierDisplayName("TIER_1500").Should().Be("501 a 1.500 cabeças");
        HeadTierCapacityLimits.GetTierDisplayName("TIER_3000").Should().Be("1.501 a 3.000 cabeças");
        HeadTierCapacityLimits.GetTierDisplayName("TIER_5000_PLUS").Should().Be("Acima de 3.000 cabeças");
    }

    [Fact]
    public void IsWithinTenantCapacity_ShouldEnforceActiveHerdLimitsAgainstTenant()
    {
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Teste Capacidade",
            CNPJ = "12.345.678/0001-99",
            Status = "Active"
        };

        // Configura faixa TIER_1500 (1500 cabeças)
        tenant.SetHeadTier(HeadTierCapacityLimits.Tier1500);

        HeadTierCapacityLimits.ResolveTenantCapacityLimit(tenant).Should().Be(1500);
        HeadTierCapacityLimits.IsWithinTenantCapacity(tenant, 0).Should().BeTrue();
        HeadTierCapacityLimits.IsWithinTenantCapacity(tenant, 1500).Should().BeTrue();
        HeadTierCapacityLimits.IsWithinTenantCapacity(tenant, 1501).Should().BeFalse();
        HeadTierCapacityLimits.IsWithinTenantCapacity(tenant, -1).Should().BeFalse();
    }
}

