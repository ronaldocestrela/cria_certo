using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Domain;
using CriaCerto.Modules.Referral.Application.Domain.Errors;
using FluentAssertions;
using Xunit;

namespace CriaCerto.Modules.Referral.UnitTests.Domain;

public class ReferralCodeTests
{
    [Fact]
    public void Create_WithValidCode_ShouldNormalizeToUpperCase()
    {
        // Arrange
        var participantId = Guid.NewGuid();

        // Act
        var result = ReferralCode.Create(participantId, "fazenda-modelo", "Campanha Instagram");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ParticipantId.Should().Be(participantId);
        result.Value.Code.Should().Be("FAZENDA-MODELO");
        result.Value.CampaignTitle.Should().Be("Campanha Instagram");
        result.Value.ClicksCount.Should().Be(0);
        result.Value.IsActive.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("AB")] // menor que 3 chars
    public void Create_WithInvalidCode_ShouldFail(string invalidCode)
    {
        // Act
        var result = ReferralCode.Create(Guid.NewGuid(), invalidCode);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ReferralCode.InvalidFormat");
    }

    [Fact]
    public void RecordClick_WhenActive_ShouldIncrementClicks()
    {
        // Arrange
        var code = ReferralCode.Create(Guid.NewGuid(), "BOIVIGOR").Value;

        // Act
        var result = code.RecordClick();

        // Assert
        result.IsSuccess.Should().BeTrue();
        code.ClicksCount.Should().Be(1);
    }

    [Fact]
    public void RecordClick_WhenInactive_ShouldFail()
    {
        // Arrange
        var code = ReferralCode.Create(Guid.NewGuid(), "BOIVIGOR").Value;
        code.Deactivate();

        // Act
        var result = code.RecordClick();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ReferralCode.Inactive");
        code.ClicksCount.Should().Be(0);
    }

    [Fact]
    public void DeactivateAndReactivate_ShouldToggleStatus()
    {
        // Arrange
        var code = ReferralCode.Create(Guid.NewGuid(), "BOIVIGOR").Value;

        // Act & Assert
        code.Deactivate().IsSuccess.Should().BeTrue();
        code.IsActive.Should().BeFalse();

        code.Reactivate().IsSuccess.Should().BeTrue();
        code.IsActive.Should().BeTrue();
    }
}
