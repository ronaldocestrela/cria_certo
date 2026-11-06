using System;
using System.Linq;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Domain;
using FluentAssertions;
using Xunit;

namespace CriaCerto.Modules.Tenancy.UnitTests.Domain;

public class LegacyPlanMigrationTests
{
    [Theory]
    [InlineData("Starter")]
    [InlineData("STARTER")]
    [InlineData("starter")]
    [InlineData("Starter Pecuária")]
    [InlineData("Starter Pecuaria")]
    [InlineData("Plano Starter")]
    public void ResolveLegacyPlan_Should_Map_Starter_To_Tier500_And_StarterModules(string planName)
    {
        var resolved = LegacyPlanMigrationResolver.ResolveLegacyPlan(planName);

        resolved.HeadTierCode.Should().Be(HeadTierCapacityLimits.Tier500);
        resolved.Capacity.Should().Be(HeadTierCapacityLimits.Tier500MaxHeads);
        resolved.SizeSegment.Should().Be(TenantSegmentationCatalog.ResolveSizeSegmentFromCapacity(HeadTierCapacityLimits.Tier500MaxHeads));
        resolved.ModuleKeys.Should().BeEquivalentTo(new[] { "Cows", "Breeding", "Calving" });
    }

    [Theory]
    [InlineData("Pro")]
    [InlineData("PRO")]
    [InlineData("pro")]
    [InlineData("Pro Fazenda")]
    [InlineData("Plano Pro")]
    [InlineData("Profissional")]
    public void ResolveLegacyPlan_Should_Map_Pro_To_Tier3000_And_ProModules(string planName)
    {
        var resolved = LegacyPlanMigrationResolver.ResolveLegacyPlan(planName);

        resolved.HeadTierCode.Should().Be(HeadTierCapacityLimits.Tier3000);
        resolved.Capacity.Should().Be(HeadTierCapacityLimits.Tier3000MaxHeads);
        resolved.SizeSegment.Should().Be(TenantSegmentationCatalog.ResolveSizeSegmentFromCapacity(HeadTierCapacityLimits.Tier3000MaxHeads));
        resolved.ModuleKeys.Should().BeEquivalentTo(new[] { "Cows", "Breeding", "Calving", "Growth", "Nutrition" });
    }

    [Theory]
    [InlineData("Enterprise")]
    [InlineData("ENTERPRISE")]
    [InlineData("enterprise")]
    [InlineData("Enterprise Confinamento")]
    [InlineData("Plano Enterprise")]
    public void ResolveLegacyPlan_Should_Map_Enterprise_To_Tier5000Plus_And_AllCommercialModules(string planName)
    {
        var resolved = LegacyPlanMigrationResolver.ResolveLegacyPlan(planName);

        resolved.HeadTierCode.Should().Be(HeadTierCapacityLimits.Tier5000Plus);
        resolved.Capacity.Should().Be(HeadTierCapacityLimits.Tier5000PlusMaxHeads);
        resolved.SizeSegment.Should().Be(TenantSegmentationCatalog.ResolveSizeSegmentFromCapacity(HeadTierCapacityLimits.Tier5000PlusMaxHeads));
        resolved.ModuleKeys.Should().BeEquivalentTo(HeadTierCapacityLimits.AllCommercialModules);
        resolved.ModuleKeys.Should().Contain("Sanitary");
        resolved.ModuleKeys.Should().Contain("Analytics");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("UnknownPlan")]
    [InlineData("Trial")]
    public void ResolveLegacyPlan_Should_Fallback_To_Starter_For_Unknown_Or_Null_Plans(string? planName)
    {
        var resolved = LegacyPlanMigrationResolver.ResolveLegacyPlan(planName);

        resolved.HeadTierCode.Should().Be(HeadTierCapacityLimits.Tier500);
        resolved.Capacity.Should().Be(HeadTierCapacityLimits.Tier500MaxHeads);
        resolved.SizeSegment.Should().Be(TenantSegmentationCatalog.ResolveSizeSegmentFromCapacity(HeadTierCapacityLimits.Tier500MaxHeads));
        resolved.ModuleKeys.Should().BeEquivalentTo(new[] { "Cows", "Breeding", "Calving" });
    }

    [Fact]
    public void Tenant_MigrateFromLegacyPlan_WhenStarter_ShouldUpdateHeadTier_Capacity_SizeSegment_And_SubscribedModules()
    {
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Legada Starter",
            CNPJ = "12.345.678/0001-90",
            CnpjNormalized = "12345678000190",
            SubscribedPlan = "Starter",
            Capacity = 500
        };

        var result = tenant.MigrateFromLegacyPlan();

        result.IsSuccess.Should().BeTrue();
        tenant.HeadTierCode.Should().Be(HeadTierCapacityLimits.Tier500);
        tenant.Capacity.Should().Be(500);
        tenant.SizeSegment.Should().Be(TenantSegmentationCatalog.ResolveSizeSegmentFromCapacity(500));
        tenant.GetSubscribedModuleKeys().Should().BeEquivalentTo(new[] { "Cows", "Breeding", "Calving" });
        tenant.SubscribedModules.Should().HaveCount(3);
        tenant.SubscribedModules.All(m => m.TenantId == tenant.Id).Should().BeTrue();
    }

    [Fact]
    public void Tenant_MigrateFromLegacyPlan_WhenPro_ShouldUpdateHeadTier_Capacity_SizeSegment_And_SubscribedModules()
    {
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Legada Pro",
            CNPJ = "23.456.789/0001-01",
            CnpjNormalized = "23456789000101",
            SubscribedPlan = "Pro Fazenda",
            Capacity = 2500
        };

        var result = tenant.MigrateFromLegacyPlan();

        result.IsSuccess.Should().BeTrue();
        tenant.HeadTierCode.Should().Be(HeadTierCapacityLimits.Tier3000);
        tenant.Capacity.Should().Be(3000);
        tenant.SizeSegment.Should().Be(TenantSegmentationCatalog.ResolveSizeSegmentFromCapacity(3000));
        tenant.GetSubscribedModuleKeys().Should().BeEquivalentTo(new[] { "Cows", "Breeding", "Calving", "Growth", "Nutrition" });
        tenant.SubscribedModules.Should().HaveCount(5);
    }

    [Fact]
    public void Tenant_MigrateFromLegacyPlan_WhenEnterprise_ShouldIncludeAllModulesAndTier5000Plus()
    {
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Legada Enterprise",
            CNPJ = "34.567.890/0001-12",
            CnpjNormalized = "34567890000112",
            SubscribedPlan = "Enterprise Confinamento",
            Capacity = 10000
        };

        var result = tenant.MigrateFromLegacyPlan();

        result.IsSuccess.Should().BeTrue();
        tenant.HeadTierCode.Should().Be(HeadTierCapacityLimits.Tier5000Plus);
        tenant.Capacity.Should().Be(10000);
        tenant.SizeSegment.Should().Be(TenantSegmentationCatalog.ResolveSizeSegmentFromCapacity(10000));
        tenant.GetSubscribedModuleKeys().Should().BeEquivalentTo(HeadTierCapacityLimits.AllCommercialModules);
        tenant.SubscribedModules.Should().HaveCount(7);
    }

    [Fact]
    public void Tenant_MigrateFromLegacyPlan_Should_Be_Idempotent()
    {
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Idempotência",
            CNPJ = "45.678.901/0001-23",
            CnpjNormalized = "45678901000123",
            SubscribedPlan = "Pro",
            Capacity = 3000
        };

        var first = tenant.MigrateFromLegacyPlan();
        first.IsSuccess.Should().BeTrue();
        var moduleCountFirst = tenant.SubscribedModules.Count;

        var second = tenant.MigrateFromLegacyPlan();
        second.IsSuccess.Should().BeTrue();
        tenant.SubscribedModules.Count.Should().Be(moduleCountFirst);
        tenant.GetSubscribedModuleKeys().Should().BeEquivalentTo(new[] { "Cows", "Breeding", "Calving", "Growth", "Nutrition" });
    }

    [Fact]
    public void Tenant_MigrateFromLegacyPlan_WithCustomOverride_ShouldPrioritizeOverride()
    {
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Upgrade Override",
            CNPJ = "56.789.012/0001-34",
            CnpjNormalized = "56789012000134",
            SubscribedPlan = "Starter",
            Capacity = 500
        };

        var result = tenant.MigrateFromLegacyPlan(legacyPlanOverride: "Enterprise");

        result.IsSuccess.Should().BeTrue();
        tenant.HeadTierCode.Should().Be(HeadTierCapacityLimits.Tier5000Plus);
        tenant.Capacity.Should().Be(10000);
        tenant.SubscribedModules.Should().HaveCount(7);
    }
}
