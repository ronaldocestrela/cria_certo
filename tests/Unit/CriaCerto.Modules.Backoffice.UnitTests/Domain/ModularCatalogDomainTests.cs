using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;
using FluentAssertions;
using Xunit;

namespace CriaCerto.Modules.Backoffice.UnitTests.Domain;

public class ModularCatalogDomainTests
{
    [Fact]
    public void HeadTierCatalog_Create_WithValidParameters_ShouldReturnSuccessAndNormalizeCode()
    {
        // Act
        var result = HeadTierCatalog.Create("tier_500", "Até 500 cabeças", 1, 500, 1);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TierCode.Should().Be("TIER_500");
        result.Value.DisplayName.Should().Be("Até 500 cabeças");
        result.Value.MinHeads.Should().Be(1);
        result.Value.MaxHeads.Should().Be(500);
        result.Value.SortOrder.Should().Be(1);
        result.Value.IsActive.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "Até 500 cabeças", 1, 500)]
    [InlineData("TIER_500", "", 1, 500)]
    [InlineData("TIER_500", "Até 500 cabeças", -1, 500)]
    [InlineData("TIER_500", "Até 500 cabeças", 0, 0)]
    [InlineData("TIER_500", "Até 500 cabeças", 600, 500)]
    public void HeadTierCatalog_Create_WithInvalidParameters_ShouldReturnFailure(
        string tierCode,
        string displayName,
        int minHeads,
        int maxHeads)
    {
        // Act
        var result = HeadTierCatalog.Create(tierCode, displayName, minHeads, maxHeads, 1);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PlanErrors.InvalidHeadTierData);
    }

    [Fact]
    public void HeadTierCatalog_UpdateDetails_ShouldUpdateProperties()
    {
        // Arrange
        var tier = HeadTierCatalog.Create("TIER_500", "Até 500 cabeças", 1, 500, 1).Value;

        // Act
        var updateResult = tier.UpdateDetails("Até 600 cabeças", 1, 600, 2, false);

        // Assert
        updateResult.IsSuccess.Should().BeTrue();
        tier.DisplayName.Should().Be("Até 600 cabeças");
        tier.MaxHeads.Should().Be(600);
        tier.SortOrder.Should().Be(2);
        tier.IsActive.Should().BeFalse();
    }

    [Fact]
    public void ModuleCatalog_Create_WithValidParameters_ShouldReturnSuccess()
    {
        // Act
        var result = ModuleCatalog.Create(
            "Cows",
            "Gestão do Plantel (Base)",
            "Gestão cadastral do rebanho, lotes básicos, unidades produtivas e dashboard executivo.",
            "bi-cow",
            isCore: true,
            sortOrder: 1);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ModuleKey.Should().Be("Cows");
        result.Value.Name.Should().Be("Gestão do Plantel (Base)");
        result.Value.IsCore.Should().BeTrue();
        result.Value.IsActive.Should().BeTrue();
        result.Value.SortOrder.Should().Be(1);
        result.Value.TierPrices.Should().BeEmpty();
    }

    [Fact]
    public void ModuleCatalog_UpdateDetails_WhenDeactivatingCoreModule_ShouldReturnFailure()
    {
        // Arrange
        var coreModule = ModuleCatalog.Create(
            "Cows",
            "Gestão do Plantel (Base)",
            "Módulo obrigatório",
            "bi-cow",
            isCore: true,
            sortOrder: 1).Value;

        // Act
        var result = coreModule.UpdateDetails(
            "Gestão do Plantel (Base)",
            "Módulo obrigatório",
            "bi-cow",
            isCore: true,
            sortOrder: 1,
            isActive: false);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PlanErrors.CannotDeactivateCoreModule);
        coreModule.IsActive.Should().BeTrue();
    }

    [Fact]
    public void ModuleCatalog_CreateTierPriceDraft_ShouldCreateDraftWithIncrementalVersionPerTier()
    {
        // Arrange
        var module = ModuleCatalog.Create("Breeding", "Reprodução & IATF", "Protocolos IATF e DG", "bi-heart-pulse", false, 2).Value;
        var tier500Id = Guid.NewGuid();
        var tier1500Id = Guid.NewGuid();

        // Act
        var price500Result = module.CreateTierPriceDraft(tier500Id, 49m, 39.20m, "price_breeding_500_m", "price_breeding_500_a");
        var price1500Result = module.CreateTierPriceDraft(tier1500Id, 89m, 71.20m, "price_breeding_1500_m", "price_breeding_1500_a");

        // Assert
        price500Result.IsSuccess.Should().BeTrue();
        price500Result.Value.VersionNumber.Should().Be(1);
        price500Result.Value.Status.Should().Be(PlanVersionStatus.Draft);
        price500Result.Value.MonthlyPrice.Should().Be(49m);
        price500Result.Value.AnnualPriceMonthly.Should().Be(39.20m);

        price1500Result.IsSuccess.Should().BeTrue();
        price1500Result.Value.VersionNumber.Should().Be(1);
        module.TierPrices.Should().HaveCount(2);
    }

    [Fact]
    public void ModuleCatalog_CreateTierPriceDraft_WhenDraftAlreadyExistsForSameTier_ShouldReturnFailure()
    {
        // Arrange
        var module = ModuleCatalog.Create("Breeding", "Reprodução & IATF", "Protocolos IATF e DG", "bi-heart-pulse", false, 2).Value;
        var tier500Id = Guid.NewGuid();
        module.CreateTierPriceDraft(tier500Id, 49m, 39.20m);

        // Act
        var duplicateDraftResult = module.CreateTierPriceDraft(tier500Id, 59m, 47.20m);

        // Assert
        duplicateDraftResult.IsFailure.Should().BeTrue();
        duplicateDraftResult.Error.Should().Be(PlanErrors.DraftAlreadyExists);
    }

    [Fact]
    public void ModuleCatalog_PublishTierPrice_ShouldPublishTargetAndDeprecatePreviousPublishedVersionOfSameTierOnly()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var module = ModuleCatalog.Create("Sanitary", "Sanitário & Carência", "Manejo sanitário", "bi-shield-plus", false, 5).Value;
        var tier500Id = Guid.NewGuid();
        var tier1500Id = Guid.NewGuid();

        var v1Tier500 = module.CreateTierPriceDraft(tier500Id, 49m, 39.20m).Value;
        var v1Tier1500 = module.CreateTierPriceDraft(tier1500Id, 89m, 71.20m).Value;

        module.PublishTierPrice(v1Tier500.Id, adminId, "Lançamento v1").IsSuccess.Should().BeTrue();
        module.PublishTierPrice(v1Tier1500.Id, adminId, "Lançamento v1").IsSuccess.Should().BeTrue();

        var v2Tier500 = module.CreateTierPriceDraft(tier500Id, 55m, 44m).Value;
        v2Tier500.VersionNumber.Should().Be(2);

        // Act
        var publishV2Result = module.PublishTierPrice(v2Tier500.Id, adminId, "Reajuste Faixa 500");

        // Assert
        publishV2Result.IsSuccess.Should().BeTrue();
        v2Tier500.Status.Should().Be(PlanVersionStatus.Published);
        v2Tier500.PublishedByAdminId.Should().Be(adminId);
        v1Tier500.Status.Should().Be(PlanVersionStatus.Deprecated);
        v1Tier1500.Status.Should().Be(PlanVersionStatus.Published);
    }

    [Fact]
    public void ModuleTierPrice_UpdateDraft_WhenPublished_ShouldReturnFailure()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var module = ModuleCatalog.Create("Nutrition", "Nutrição & Confinamento", "Trato TMR", "bi-basket", false, 6).Value;
        var tier500Id = Guid.NewGuid();
        var price = module.CreateTierPriceDraft(tier500Id, 79m, 63.20m).Value;
        module.PublishTierPrice(price.Id, adminId, "Publicação inicial");

        // Act
        var updateResult = price.UpdateDraft(99m, 79.20m, "price_new_m", "price_new_a");

        // Assert
        updateResult.IsFailure.Should().BeTrue();
        updateResult.Error.Should().Be(PlanErrors.PublishedVersionImmutable);
    }
}

