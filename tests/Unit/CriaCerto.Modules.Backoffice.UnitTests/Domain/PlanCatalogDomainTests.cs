using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;
using FluentAssertions;
using Xunit;

namespace CriaCerto.Modules.Backoffice.UnitTests.Domain;

public class PlanCatalogDomainTests
{
    [Fact]
    public void Create_WithValidParameters_ShouldReturnSuccess()
    {
        // Act
        var result = PlanCatalog.Create("starter", "Plano Starter", "Para pequenas fazendas", "PeDistributed");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Code.Should().Be("starter");
        result.Value.Name.Should().Be("Plano Starter");
        result.Value.Description.Should().Be("Para pequenas fazendas");
        result.Value.Versions.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithInvalidParameters_ShouldReturnFailure()
    {
        // Act
        var result = PlanCatalog.Create("", "Name", "Desc");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PlanErrors.InvalidPlanData);
    }

    [Fact]
    public void CreateVersion_ShouldCreateDraftVersion_WithIncrementalVersionNumber()
    {
        // Arrange
        var plan = PlanCatalog.Create("pro", "Plano Pro", "Para médias fazendas").Value;

        // Act
        var versionResult = plan.CreateVersion("v1.0 - Lançamento", 299.90m, 249.90m, 2500);

        // Assert
        versionResult.IsSuccess.Should().BeTrue();
        versionResult.Value.VersionNumber.Should().Be(1);
        versionResult.Value.Status.Should().Be(PlanVersionStatus.Draft);
        versionResult.Value.MonthlyPrice.Should().Be(299.90m);
        versionResult.Value.HeadCapacityLimit.Should().Be(2500);
        plan.Versions.Should().HaveCount(1);
    }

    [Fact]
    public void CreateVersion_WhenDraftAlreadyExists_ShouldReturnFailure()
    {
        // Arrange
        var plan = PlanCatalog.Create("pro", "Plano Pro", "Para médias fazendas").Value;
        plan.CreateVersion("v1.0", 299.90m, 249.90m, 2500);

        // Act
        var secondVersionResult = plan.CreateVersion("v2.0", 399.90m, 349.90m, 3000);

        // Assert
        secondVersionResult.IsFailure.Should().BeTrue();
        secondVersionResult.Error.Should().Be(PlanErrors.DraftAlreadyExists);
    }

    [Fact]
    public void PublishVersion_ShouldSetStatusToPublished_AndDeprecatePreviousPublishedVersion()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var plan = PlanCatalog.Create("pro", "Plano Pro", "Para médias fazendas").Value;

        // Create & Publish v1
        var v1 = plan.CreateVersion("v1.0", 200m, 180m, 1000).Value;
        plan.PublishVersion(v1.Id, adminId, "Aprovado por FinanceOps").IsSuccess.Should().BeTrue();

        v1.Status.Should().Be(PlanVersionStatus.Published);
        v1.PublishedByAdminId.Should().Be(adminId);

        // Create v2 Draft
        var v2 = plan.CreateVersion("v2.0", 250m, 220m, 1500).Value;

        // Act - Publish v2
        var publishV2Result = plan.PublishVersion(v2.Id, adminId, "Reajuste anual").IsSuccess.Should().BeTrue();

        // Assert
        v2.Status.Should().Be(PlanVersionStatus.Published);
        v1.Status.Should().Be(PlanVersionStatus.Deprecated);
    }

    [Fact]
    public void UpdateDraft_WhenVersionIsPublished_ShouldReturnFailure()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var plan = PlanCatalog.Create("pro", "Plano Pro", "Para médias fazendas").Value;
        var v1 = plan.CreateVersion("v1.0", 200m, 180m, 1000).Value;
        plan.PublishVersion(v1.Id, adminId);

        // Act
        var updateResult = v1.UpdateDraft("v1.0 Editado", 300m, 280m, 2000, null, null);

        // Assert
        updateResult.IsFailure.Should().BeTrue();
        updateResult.Error.Should().Be(PlanErrors.PublishedVersionImmutable);
    }
}
