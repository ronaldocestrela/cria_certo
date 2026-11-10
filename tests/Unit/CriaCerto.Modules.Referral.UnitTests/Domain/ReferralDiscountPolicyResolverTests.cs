using CriaCerto.Modules.Referral.Application.Domain;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Domain.Services;
using FluentAssertions;
using Xunit;

namespace CriaCerto.Modules.Referral.UnitTests.Domain;

public class ReferralDiscountPolicyResolverTests
{
    [Fact]
    public void Resolve_WhenHeadTierPolicyExists_ShouldReturnHeadTierPolicy()
    {
        // Arrange
        var tierPolicy = PlanReferralDiscountPolicy.CreateForHeadTier(
            Guid.NewGuid(), "TIER_500", DiscountType.Percentage, 15m).Value;
        var globalPolicy = PlanReferralDiscountPolicy.CreateGlobalDefault(
            DiscountType.Percentage, 10m).Value;
        var legacyPolicy = PlanReferralDiscountPolicy.Create(
            Guid.NewGuid(), "STARTER", DiscountType.FixedValue, 50m).Value;

        var policies = new List<PlanReferralDiscountPolicy> { tierPolicy, globalPolicy, legacyPolicy };
        var resolver = new ReferralDiscountPolicyResolver();

        // Act
        var resolved = resolver.Resolve(policies, headTierCode: "TIER_500", legacyPlanCode: "STARTER");

        // Assert
        resolved.Should().NotBeNull();
        resolved!.DiscountScope.Should().Be(DiscountPolicyScope.HeadTier);
        resolved.HeadTierCode.Should().Be("TIER_500");
        resolved.DiscountValue.Should().Be(15m);
    }

    [Fact]
    public void Resolve_WhenNoHeadTierPolicyMatches_ShouldFallbackToGlobalDefault()
    {
        // Arrange
        var tierPolicy = PlanReferralDiscountPolicy.CreateForHeadTier(
            Guid.NewGuid(), "TIER_3000", DiscountType.Percentage, 25m).Value;
        var globalPolicy = PlanReferralDiscountPolicy.CreateGlobalDefault(
            DiscountType.Percentage, 10m).Value;
        var legacyPolicy = PlanReferralDiscountPolicy.Create(
            Guid.NewGuid(), "STARTER", DiscountType.FixedValue, 50m).Value;

        var policies = new List<PlanReferralDiscountPolicy> { tierPolicy, globalPolicy, legacyPolicy };
        var resolver = new ReferralDiscountPolicyResolver();

        // Act
        var resolved = resolver.Resolve(policies, headTierCode: "TIER_500", legacyPlanCode: "STARTER");

        // Assert
        resolved.Should().NotBeNull();
        resolved!.DiscountScope.Should().Be(DiscountPolicyScope.GlobalDefault);
        resolved.DiscountValue.Should().Be(10m);
    }

    [Fact]
    public void Resolve_WhenNoHeadTierAndNoGlobalDefault_ShouldFallbackToLegacyPlan()
    {
        // Arrange
        var legacyPolicy = PlanReferralDiscountPolicy.Create(
            Guid.NewGuid(), "STARTER", DiscountType.FixedValue, 50m).Value;

        var policies = new List<PlanReferralDiscountPolicy> { legacyPolicy };
        var resolver = new ReferralDiscountPolicyResolver();

        // Act
        var resolved = resolver.Resolve(policies, headTierCode: "TIER_UNKNOWN", legacyPlanCode: "STARTER");

        // Assert
        resolved.Should().NotBeNull();
        resolved!.DiscountScope.Should().Be(DiscountPolicyScope.LegacyPlan);
        resolved.PlanCode.Should().Be("STARTER");
        resolved.DiscountValue.Should().Be(50m);
    }

    [Fact]
    public void Resolve_WhenPolicyIsInactive_ShouldSkipAndFallbackToNextPriority()
    {
        // Arrange
        var inactiveTierPolicy = PlanReferralDiscountPolicy.CreateForHeadTier(
            Guid.NewGuid(), "TIER_500", DiscountType.Percentage, 20m).Value;
        inactiveTierPolicy.Deactivate();

        var globalPolicy = PlanReferralDiscountPolicy.CreateGlobalDefault(
            DiscountType.Percentage, 12m).Value;

        var policies = new List<PlanReferralDiscountPolicy> { inactiveTierPolicy, globalPolicy };
        var resolver = new ReferralDiscountPolicyResolver();

        // Act
        var resolved = resolver.Resolve(policies, headTierCode: "TIER_500", legacyPlanCode: null);

        // Assert
        resolved.Should().NotBeNull();
        resolved!.DiscountScope.Should().Be(DiscountPolicyScope.GlobalDefault);
        resolved.DiscountValue.Should().Be(12m);
    }

    [Fact]
    public void Resolve_WhenNoPolicyMatches_ShouldReturnNull()
    {
        // Arrange
        var policies = new List<PlanReferralDiscountPolicy>();
        var resolver = new ReferralDiscountPolicyResolver();

        // Act
        var resolved = resolver.Resolve(policies, headTierCode: "TIER_9999", legacyPlanCode: "NON_EXISTING");

        // Assert
        resolved.Should().BeNull();
    }
}

