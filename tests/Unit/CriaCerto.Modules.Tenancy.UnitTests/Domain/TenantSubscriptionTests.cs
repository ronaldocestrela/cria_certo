using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Domain.Errors;
using FluentAssertions;
using Xunit;

namespace CriaCerto.Modules.Tenancy.UnitTests.Domain;

public class TenantSubscriptionTests
{
    [Fact]
    public void ApplyPlanChange_WhenUpgrade_AppliesImmediately()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var currentPlanId = Guid.NewGuid();
        var currentVersionId = Guid.NewGuid();
        var subscription = TenantSubscription.CreateInitial(tenantId, currentPlanId, currentVersionId, "starter", "Starter", 1, 500);

        var newPlanId = Guid.NewGuid();
        var newVersionId = Guid.NewGuid();

        // Act
        var result = subscription.ApplyPlanChange(
            newPlanId, newVersionId, "pro", "Pro", 1, 2500, 20, 10,
            currentHeadCount: 400, currentActiveUsers: 2, currentProductionUnits: 1);

        // Assert
        result.IsSuccess.Should().BeTrue();
        subscription.PlanVersionId.Should().Be(newVersionId);
        subscription.PlanCode.Should().Be("pro");
        subscription.MaxHeadCapacity.Should().Be(2500);
        subscription.Status.Should().Be(SubscriptionStatus.Active);
        subscription.GracePeriodEndsAtUtc.Should().BeNull();
    }

    [Fact]
    public void ApplyPlanChange_WhenSameVersion_ReturnsFailure()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var currentPlanId = Guid.NewGuid();
        var currentVersionId = Guid.NewGuid();
        var subscription = TenantSubscription.CreateInitial(tenantId, currentPlanId, currentVersionId, "starter", "Starter", 1, 500);

        // Act
        var result = subscription.ApplyPlanChange(
            currentPlanId, currentVersionId, "starter", "Starter", 1, 500, 10, 5,
            currentHeadCount: 200, currentActiveUsers: 2, currentProductionUnits: 1);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TenancyErrors.AlreadySubscribedToPlanVersion);
    }

    [Fact]
    public void ApplyPlanChange_WhenDowngradeWithExcessUsage_ActivatesGracePeriod()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var proPlanId = Guid.NewGuid();
        var proVersionId = Guid.NewGuid();
        var subscription = TenantSubscription.CreateInitial(tenantId, proPlanId, proVersionId, "pro", "Pro", 1, 2500);

        var starterPlanId = Guid.NewGuid();
        var starterVersionId = Guid.NewGuid();

        // Tenant currently has 800 heads (starter limit is 500)
        // Act
        var result = subscription.ApplyPlanChange(
            starterPlanId, starterVersionId, "starter", "Starter", 1, 500, 5, 2,
            currentHeadCount: 800, currentActiveUsers: 2, currentProductionUnits: 1);

        // Assert
        result.IsSuccess.Should().BeTrue();
        subscription.Status.Should().Be(SubscriptionStatus.GracePeriodActive);
        subscription.PlanVersionId.Should().Be(proVersionId); // Remains on pro version during grace period
        subscription.PendingPlanVersionId.Should().Be(starterVersionId);
        subscription.GracePeriodEndsAtUtc.Should().NotBeNull();
        subscription.GracePeriodEndsAtUtc!.Value.Should().BeAfter(DateTime.UtcNow.AddDays(13));
    }

    [Fact]
    public void ResolveGracePeriod_WhenUsageIsReducedWithinLimits_FinalizesDowngrade()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var proPlanId = Guid.NewGuid();
        var proVersionId = Guid.NewGuid();
        var subscription = TenantSubscription.CreateInitial(tenantId, proPlanId, proVersionId, "pro", "Pro", 1, 2500);

        var starterPlanId = Guid.NewGuid();
        var starterVersionId = Guid.NewGuid();

        // Start Grace Period
        subscription.ApplyPlanChange(
            starterPlanId, starterVersionId, "starter", "Starter", 1, 500, 5, 2,
            currentHeadCount: 800, currentActiveUsers: 2, currentProductionUnits: 1);

        // Act: User reduced head count to 450 (within starter limit 500)
        var resolveResult = subscription.ResolveGracePeriod(
            starterPlanId, starterVersionId, "starter", "Starter", 1, 500, 5, 2,
            currentHeadCount: 450, currentActiveUsers: 2, currentProductionUnits: 1);

        // Assert
        resolveResult.IsSuccess.Should().BeTrue();
        subscription.Status.Should().Be(SubscriptionStatus.Active);
        subscription.PlanVersionId.Should().Be(starterVersionId);
        subscription.MaxHeadCapacity.Should().Be(500);
        subscription.GracePeriodEndsAtUtc.Should().BeNull();
        subscription.PendingPlanVersionId.Should().BeNull();
    }
}
