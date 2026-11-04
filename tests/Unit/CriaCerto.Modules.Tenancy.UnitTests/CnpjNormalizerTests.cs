using CriaCerto.Modules.Tenancy.Application.Domain;
using FluentAssertions;

namespace CriaCerto.Modules.Tenancy.UnitTests;

public class CnpjNormalizerTests
{
    [Theory]
    [InlineData("12.345.678/0001-90", "12345678000190")]
    [InlineData("123.456.789-09", "12345678909")]
    [InlineData("", "")]
    public void Normalize_Should_Extract_Digits(string input, string expected)
    {
        CnpjNormalizer.Normalize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("12.345.678/0001-90", true)]
    [InlineData("123.456.789-09", true)]
    [InlineData("123", false)]
    [InlineData("", false)]
    public void IsValidCnpjOrCpf_Should_Validate_Length(string input, bool expected)
    {
        CnpjNormalizer.IsValidCnpjOrCpf(input).Should().Be(expected);
    }
}

public class PlanCapacityLimitsTests
{
    [Theory]
    [InlineData("Starter", 500, true)]
    [InlineData("Starter", 501, false)]
    [InlineData("Pro", 2500, true)]
    [InlineData("Enterprise", int.MaxValue, true)]
    [InlineData("TIER_500", 500, true)]
    [InlineData("TIER_500", 501, false)]
    [InlineData("TIER_1500", 1500, true)]
    [InlineData("TIER_1500", 1501, false)]
    [InlineData("TIER_3000", 3000, true)]
    [InlineData("TIER_3000", 3001, false)]
    [InlineData("TIER_5000_PLUS", 10000, true)]
    public void IsCapacityWithinPlan_Should_Use_Catalog_Limits(string planOrTier, int capacity, bool expected)
    {
        PlanCapacityLimits.IsCapacityWithinPlan(planOrTier, capacity).Should().Be(expected);
    }

    [Theory]
    [InlineData(HeadTierCapacityLimits.Tier500, 500, true)]
    [InlineData(HeadTierCapacityLimits.Tier500, 501, false)]
    [InlineData(HeadTierCapacityLimits.Tier1500, 1500, true)]
    [InlineData(HeadTierCapacityLimits.Tier1500, 1501, false)]
    [InlineData(HeadTierCapacityLimits.Tier3000, 3000, true)]
    [InlineData(HeadTierCapacityLimits.Tier3000, 3001, false)]
    [InlineData(HeadTierCapacityLimits.Tier5000Plus, 10000, true)]
    [InlineData(HeadTierCapacityLimits.Tier5000Plus, 10001, false)]
    public void HeadTierCapacityLimits_IsCapacityWithinTier_Should_Validate_Against_Tier_MaxHeads(
        string tierCode,
        int capacity,
        bool expected)
    {
        HeadTierCapacityLimits.IsCapacityWithinTier(tierCode, capacity).Should().Be(expected);
    }

    [Fact]
    public void HeadTierCapacityLimits_IsWithinTenantCapacity_Should_Consult_Tenant_Capacity_Directly()
    {
        var tenant = new Tenant { Id = Guid.NewGuid() };
        tenant.SetHeadTier(HeadTierCapacityLimits.Tier1500).IsSuccess.Should().BeTrue();

        HeadTierCapacityLimits.ResolveTenantCapacityLimit(tenant).Should().Be(1500);
        HeadTierCapacityLimits.IsWithinTenantCapacity(tenant, 1500).Should().BeTrue();
        HeadTierCapacityLimits.IsWithinTenantCapacity(tenant, 1501).Should().BeFalse();
    }
}
