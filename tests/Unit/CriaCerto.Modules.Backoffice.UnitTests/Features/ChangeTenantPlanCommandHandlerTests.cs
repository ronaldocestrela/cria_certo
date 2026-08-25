using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Features.Tenants.Commands;
using CriaCerto.Modules.Backoffice.Application.Features.Tenants.Queries;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Domain.Errors;
using FluentAssertions;
using Xunit;

namespace CriaCerto.Modules.Backoffice.UnitTests.Features;

public class ChangeTenantPlanCommandHandlerTests
{
    private static PlanVersion CreatePublishedVersion(
        string versionName,
        int headCapacityLimit,
        IEnumerable<PlanFeature>? features = null)
    {
        var createResult = PlanVersion.CreateDraft(
            planCatalogId: Guid.NewGuid(),
            versionNumber: 1,
            versionName: versionName,
            monthlyPrice: 100m,
            annualPriceMonthly: 90m,
            headCapacityLimit: headCapacityLimit,
            features: features);

        createResult.IsSuccess.Should().BeTrue();
        var version = createResult.Value;
        version.Publish(Guid.NewGuid()).IsSuccess.Should().BeTrue();
        return version;
    }

    [Fact]
    public async Task Handle_WhenJustificationTooShort_FailsValidation()
    {
        // Arrange
        var validator = new ChangeTenantPlanCommandValidator();
        var command = new ChangeTenantPlanCommand(
            TenantId: Guid.NewGuid(),
            TargetPlanVersionId: Guid.NewGuid(),
            AdminUserId: Guid.NewGuid(),
            Justification: "Short" // < 15 chars
        );

        // Act
        var result = await validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Justification");
    }

    [Fact]
    public async Task Handle_WhenUpgrade_AppliesImmediately()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = new Tenant
        {
            Id = tenantId,
            Name = "Fazenda Santa Maria",
            SubscribedPlan = "Starter",
            Capacity = 500
        };

        var targetVersion = CreatePublishedVersion("Pro 2026.1", 2500);
        var targetVersionId = targetVersion.Id;

        TenantSubscriptionHistory? savedHistory = null;

        var handler = new ChangeTenantPlanCommandHandler(
            tenantLookup: id => Task.FromResult<Tenant?>(id == tenantId ? tenant : null),
            planVersionLookup: id => Task.FromResult<PlanVersion?>(id == targetVersionId ? targetVersion : null),
            saveHistory: h => { savedHistory = h; return Task.CompletedTask; }
        );

        var command = new ChangeTenantPlanCommand(
            TenantId: tenantId,
            TargetPlanVersionId: targetVersionId,
            AdminUserId: Guid.NewGuid(),
            Justification: "Upgrade solicitado pelo produtor via canal de atendimento comercial."
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.GracePeriodActivated.Should().BeFalse();
        result.Value.SubscriptionStatus.Should().Be("Active");
        tenant.Capacity.Should().Be(2500);
        tenant.SubscribedPlan.Should().Be("Pro 2026.1");

        savedHistory.Should().NotBeNull();
        savedHistory!.ActionType.Should().Be(SubscriptionActionType.Upgrade);
    }

    [Fact]
    public async Task Handle_WhenDowngradeExceedsCapacity_ActivatesGracePeriod()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = new Tenant
        {
            Id = tenantId,
            Name = "Fazenda Boa Vista",
            SubscribedPlan = "Pro",
            Capacity = 1200 // > Starter limit of 500
        };

        var starterVersion = CreatePublishedVersion("Starter 2026.1", 500);
        var starterVersionId = starterVersion.Id;

        TenantSubscriptionHistory? savedHistory = null;

        var handler = new ChangeTenantPlanCommandHandler(
            tenantLookup: id => Task.FromResult<Tenant?>(id == tenantId ? tenant : null),
            planVersionLookup: id => Task.FromResult<PlanVersion?>(id == starterVersionId ? starterVersion : null),
            saveHistory: h => { savedHistory = h; return Task.CompletedTask; }
        );

        var command = new ChangeTenantPlanCommand(
            TenantId: tenantId,
            TargetPlanVersionId: starterVersionId,
            AdminUserId: Guid.NewGuid(),
            Justification: "Downgrade para plano Starter solicitado pelo cliente por redução de lote."
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.GracePeriodActivated.Should().BeTrue();
        result.Value.SubscriptionStatus.Should().Be("GracePeriodActive");
        result.Value.GracePeriodEndsAtUtc.Should().NotBeNull();

        savedHistory.Should().NotBeNull();
        savedHistory!.ActionType.Should().Be(SubscriptionActionType.DowngradeGracePeriodStarted);
    }

    [Fact]
    public async Task Handle_PreviewQuery_ReturnsExpectedImpactDeltas()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = new Tenant
        {
            Id = tenantId,
            Name = "Fazenda Modelo",
            SubscribedPlan = "Starter",
            Capacity = 500
        };

        var targetVersion = CreatePublishedVersion(
            "Enterprise 2026",
            10000,
            new[]
            {
                PlanFeature.Create("breeding.iatf", "Gestão de IATF"),
                PlanFeature.Create("nutrition.tmr", "Controle de Trato")
            });
        var targetVersionId = targetVersion.Id;

        var queryHandler = new PreviewTenantPlanChangeQueryHandler(
            tenantLookup: id => Task.FromResult<Tenant?>(id == tenantId ? tenant : null),
            planVersionLookup: id => Task.FromResult<PlanVersion?>(id == targetVersionId ? targetVersion : null)
        );

        var query = new PreviewTenantPlanChangeQuery(tenantId, targetVersionId);

        // Act
        var result = await queryHandler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.DeltaHeadCapacity.Should().Be(9500);
        result.Value.RequiresGracePeriod.Should().BeFalse();
        result.Value.AddedFeatures.Should().Contain("Gestão de IATF");
    }
}
