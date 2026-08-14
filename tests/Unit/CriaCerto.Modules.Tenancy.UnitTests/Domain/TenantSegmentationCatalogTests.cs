using CriaCerto.Modules.Tenancy.Application.Domain;
using FluentAssertions;
using Xunit;

namespace CriaCerto.Modules.Tenancy.UnitTests.Domain;

public class TenantSegmentationCatalogTests
{
    [Theory]
    [InlineData(50, TenantSegmentationCatalog.SizeSegments.Micro)]
    [InlineData(99, TenantSegmentationCatalog.SizeSegments.Micro)]
    [InlineData(100, TenantSegmentationCatalog.SizeSegments.Small)]
    [InlineData(499, TenantSegmentationCatalog.SizeSegments.Small)]
    [InlineData(500, TenantSegmentationCatalog.SizeSegments.Medium)]
    [InlineData(2499, TenantSegmentationCatalog.SizeSegments.Medium)]
    [InlineData(2500, TenantSegmentationCatalog.SizeSegments.Large)]
    [InlineData(10000, TenantSegmentationCatalog.SizeSegments.Large)]
    public void ResolveSizeSegmentFromCapacity_Should_Map_Correctly(int capacity, string expected)
    {
        TenantSegmentationCatalog.ResolveSizeSegmentFromCapacity(capacity).Should().Be(expected);
    }

    [Theory]
    [InlineData("MT", TenantSegmentationCatalog.CommercialRegions.CentroOeste)]
    [InlineData("sp", TenantSegmentationCatalog.CommercialRegions.Sudeste)]
    [InlineData("BA", TenantSegmentationCatalog.CommercialRegions.Nordeste)]
    [InlineData("RS", TenantSegmentationCatalog.CommercialRegions.Sul)]
    [InlineData("AM", TenantSegmentationCatalog.CommercialRegions.Norte)]
    public void ResolveCommercialRegionFromState_Should_Map_Uf_To_Region(string state, string expected)
    {
        TenantSegmentationCatalog.ResolveCommercialRegionFromState(state).Should().Be(expected);
    }

    [Fact]
    public void ResolveCommercialRegionFromState_Should_Default_To_CentroOeste_For_Unknown_Uf()
    {
        TenantSegmentationCatalog.ResolveCommercialRegionFromState("XX")
            .Should().Be(TenantSegmentationCatalog.CommercialRegions.CentroOeste);
    }

    [Theory]
    [InlineData(TenantSegmentationCatalog.SizeSegments.Micro)]
    [InlineData(TenantSegmentationCatalog.CommercialRegions.Sudeste)]
    [InlineData(TenantSegmentationCatalog.ProductiveProfiles.Confinamento)]
    [InlineData(TenantSegmentationCatalog.ChurnRisks.High)]
    public void ValidateSegmentation_Should_Succeed_For_Valid_Values(string value)
    {
        TenantSegmentationCatalog.ValidateSizeSegment(value).IsSuccess.Should().Be(value is TenantSegmentationCatalog.SizeSegments.Micro);
        TenantSegmentationCatalog.ValidateCommercialRegion(value).IsSuccess.Should().Be(value is TenantSegmentationCatalog.CommercialRegions.Sudeste);
        TenantSegmentationCatalog.ValidateProductiveProfile(value).IsSuccess.Should().Be(value is TenantSegmentationCatalog.ProductiveProfiles.Confinamento);
        TenantSegmentationCatalog.ValidateChurnRisk(value).IsSuccess.Should().Be(value is TenantSegmentationCatalog.ChurnRisks.High);
    }

    [Fact]
    public void ValidateSegmentation_Should_Fail_For_Invalid_Values()
    {
        TenantSegmentationCatalog.ValidateSizeSegment("Huge").IsFailure.Should().BeTrue();
        TenantSegmentationCatalog.ValidateCommercialRegion("Invalid").IsFailure.Should().BeTrue();
        TenantSegmentationCatalog.ValidateProductiveProfile("Invalid").IsFailure.Should().BeTrue();
        TenantSegmentationCatalog.ValidateChurnRisk("Invalid").IsFailure.Should().BeTrue();
    }

    [Fact]
    public void GenerateTagSlug_Should_Normalize_Name()
    {
        TenantSegmentationCatalog.GenerateTagSlug("CS Risco Churn")
            .Should().Be("cs-risco-churn");
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(150, 100)]
    [InlineData(-5, 20)]
    [InlineData(50, 50)]
    public void ClampPageSize_Should_Enforce_Bounds(int input, int expected)
    {
        TenantSegmentationCatalog.ClampPageSize(input).Should().Be(expected);
    }
}
