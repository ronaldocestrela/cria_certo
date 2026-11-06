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

    [Fact]
    public async Task Handle_WhenModularUpgrade_AppliesHeadTierAndModules()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = new Tenant
        {
            Id = tenantId,
            Name = "Fazenda Santa Maria",
            HeadTierCode = "TIER_500",
            Capacity = 500,
            SubscribedPlan = "TIER_500 Modular"
        };
        tenant.SetSubscribedModules(new[] { "Cows" });

        TenantSubscriptionHistory? savedHistory = null;
        var handler = new ChangeTenantPlanCommandHandler(
            tenantLookup: id => Task.FromResult<Tenant?>(id == tenantId ? tenant : null),
            planVersionLookup: _ => Task.FromResult<PlanVersion?>(null),
            saveHistory: h => { savedHistory = h; return Task.CompletedTask; }
        );

        var command = new ChangeTenantPlanCommand(
            TenantId: tenantId,
            TargetPlanVersionId: null,
            AdminUserId: Guid.NewGuid(),
            Justification: "Upgrade modular para faixa 3000 com módulos adicionais de sanidade e nutrição.",
            HeadTierCode: "TIER_3000",
            SubscribedModules: new[] { "Cows", "Sanitary", "Nutrition" }
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.HeadTierCode.Should().Be("TIER_3000");
        result.Value.HeadCapacity.Should().Be(3000);
        result.Value.SubscribedModules.Should().BeEquivalentTo(new[] { "Cows", "Sanitary", "Nutrition" });
        tenant.HeadTierCode.Should().Be("TIER_3000");
        tenant.Capacity.Should().Be(3000);
        tenant.GetSubscribedModuleKeys().Should().Contain(new[] { "Cows", "Sanitary", "Nutrition" });
        savedHistory.Should().NotBeNull();
        savedHistory!.ActionType.Should().Be(SubscriptionActionType.PlanChanged);
    }

    [Fact]
    public async Task Handle_WhenModularSelectionOmitsCows_EnforcesCowsCoreModule()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = new Tenant
        {
            Id = tenantId,
            Name = "Fazenda Alvorada",
            HeadTierCode = "TIER_500",
            Capacity = 500
        };

        var handler = new ChangeTenantPlanCommandHandler(
            tenantLookup: id => Task.FromResult<Tenant?>(id == tenantId ? tenant : null),
            planVersionLookup: _ => Task.FromResult<PlanVersion?>(null),
            saveHistory: _ => Task.CompletedTask
        );

        var command = new ChangeTenantPlanCommand(
            TenantId: tenantId,
            TargetPlanVersionId: null,
            AdminUserId: Guid.NewGuid(),
            Justification: "Tentativa de selecionar apenas Breeding sem Cows pela interface.",
            HeadTierCode: "TIER_1500",
            SubscribedModules: new[] { "Breeding" } // omits Cows
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SubscribedModules.Should().Contain("Cows");
        tenant.GetSubscribedModuleKeys().Should().Contain("Cows");
    }

    [Fact]
    public async Task Handle_WhenModularDowngradeExceedsCapacity_ActivatesGracePeriod()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = new Tenant
        {
            Id = tenantId,
            Name = "Fazenda Campestre",
            HeadTierCode = "TIER_3000",
            Capacity = 2800 // > TIER_500 max of 500
        };

        var handler = new ChangeTenantPlanCommandHandler(
            tenantLookup: id => Task.FromResult<Tenant?>(id == tenantId ? tenant : null),
            planVersionLookup: _ => Task.FromResult<PlanVersion?>(null),
            saveHistory: _ => Task.CompletedTask
        );

        var command = new ChangeTenantPlanCommand(
            TenantId: tenantId,
            TargetPlanVersionId: null,
            AdminUserId: Guid.NewGuid(),
            Justification: "Redução de faixa solicitada pelo cliente aguardando descarte.",
            HeadTierCode: "TIER_500",
            SubscribedModules: new[] { "Cows" }
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.GracePeriodActivated.Should().BeTrue();
        result.Value.SubscriptionStatus.Should().Be("GracePeriodActive");
        result.Value.GracePeriodEndsAtUtc.Should().NotBeNull();
        tenant.Capacity.Should().Be(2800); // unchanged during grace period
    }

    [Fact]
    public async Task Handle_WhenModularDowngradeWithForceImmediate_AppliesImmediately()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = new Tenant
        {
            Id = tenantId,
            Name = "Fazenda Campestre",
            HeadTierCode = "TIER_3000",
            Capacity = 2800
        };

        var handler = new ChangeTenantPlanCommandHandler(
            tenantLookup: id => Task.FromResult<Tenant?>(id == tenantId ? tenant : null),
            planVersionLookup: _ => Task.FromResult<PlanVersion?>(null),
            saveHistory: _ => Task.CompletedTask
        );

        var command = new ChangeTenantPlanCommand(
            TenantId: tenantId,
            TargetPlanVersionId: null,
            AdminUserId: Guid.NewGuid(),
            Justification: "Forçar redução imediata por determinação de contrato executivo.",
            ForceImmediate: true,
            HeadTierCode: "TIER_500",
            SubscribedModules: new[] { "Cows" }
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.GracePeriodActivated.Should().BeFalse();
        result.Value.SubscriptionStatus.Should().Be("Active");
        tenant.Capacity.Should().Be(500);
        tenant.HeadTierCode.Should().Be("TIER_500");
    }

    [Fact]
    public async Task Handle_PreviewModular_ReturnsAddedAndRemovedModulesAndDeltas()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = new Tenant
        {
            Id = tenantId,
            Name = "Fazenda Vitória",
            HeadTierCode = "TIER_500",
            Capacity = 500
        };
        tenant.SetSubscribedModules(new[] { "Cows", "Breeding" });

        var queryHandler = new PreviewTenantPlanChangeQueryHandler(
            tenantLookup: id => Task.FromResult<Tenant?>(id == tenantId ? tenant : null),
            planVersionLookup: _ => Task.FromResult<PlanVersion?>(null)
        );

        var query = new PreviewTenantPlanChangeQuery(
            TenantId: tenantId,
            TargetPlanVersionId: null,
            HeadTierCode: "TIER_3000",
            SelectedModules: new[] { "Cows", "Growth", "Sanitary" } // added Growth, Sanitary; removed Breeding
        );

        // Act
        var result = await queryHandler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.DeltaHeadCapacity.Should().Be(2500); // 3000 - 500
        result.Value.AddedFeatures.Should().Contain(new[] { "Growth", "Sanitary" });
        result.Value.RemovedFeatures.Should().Contain("Breeding");
        result.Value.RequiresGracePeriod.Should().BeFalse();
    }
}
