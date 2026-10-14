using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Domain;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Domain.Errors;
using FluentAssertions;
using Xunit;

namespace CriaCerto.Modules.Referral.UnitTests.Domain;

public class ReferralProgramParticipantTests
{
    [Fact]
    public void CreateCustomer_WithValidTenant_ShouldSucceed()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Act
        var result = ReferralProgramParticipant.CreateCustomer(tenantId, userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TenantId.Should().Be(tenantId);
        result.Value.UserId.Should().Be(userId);
        result.Value.ParticipantType.Should().Be(ParticipantType.Customer);
        result.Value.Status.Should().Be(ParticipantStatus.Active);
        result.Value.SuspensionReason.Should().BeNull();
        result.Value.CreatedByAdminId.Should().BeNull();
    }

    [Fact]
    public void CreateCustomer_WithoutTenantId_ShouldFail()
    {
        // Act
        var result = ReferralProgramParticipant.CreateCustomer(Guid.Empty, Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ReferralParticipant.TenantRequiredForCustomer");
    }

    [Fact]
    public void CreateCommercialPartner_FromBackoffice_ShouldSucceedWithoutTenant()
    {
        // Arrange
        var adminId = Guid.NewGuid();

        // Act
        var result = ReferralProgramParticipant.CreateCommercialPartner(adminId, userId: null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TenantId.Should().BeNull();
        result.Value.UserId.Should().BeNull();
        result.Value.ParticipantType.Should().Be(ParticipantType.CommercialPartner);
        result.Value.Status.Should().Be(ParticipantStatus.Active);
        result.Value.CreatedByAdminId.Should().Be(adminId);
    }

    [Fact]
    public void CreateCommercialPartner_WithoutAdminId_ShouldFail()
    {
        // Act
        var result = ReferralProgramParticipant.CreateCommercialPartner(Guid.Empty, null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ReferralParticipant.AdminRequiredForPartner");
    }

    [Fact]
    public void Suspend_ActiveParticipant_ShouldChangeStatusAndSetReason()
    {
        // Arrange
        var participant = ReferralProgramParticipant.CreateCustomer(Guid.NewGuid(), Guid.NewGuid()).Value;

        // Act
        var result = participant.Suspend("Inadimplência financeira prolongada");

        // Assert
        result.IsSuccess.Should().BeTrue();
        participant.Status.Should().Be(ParticipantStatus.Suspended);
        participant.SuspensionReason.Should().Be("Inadimplência financeira prolongada");
    }

    [Fact]
    public void Suspend_WithoutReason_ShouldFail()
    {
        // Arrange
        var participant = ReferralProgramParticipant.CreateCustomer(Guid.NewGuid(), Guid.NewGuid()).Value;

        // Act
        var result = participant.Suspend("   ");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ReferralParticipant.SuspensionReasonRequired");
    }

    [Fact]
    public void Reactivate_SuspendedParticipant_ShouldSucceed()
    {
        // Arrange
        var participant = ReferralProgramParticipant.CreateCustomer(Guid.NewGuid(), Guid.NewGuid()).Value;
        participant.Suspend("Motivo temporário");

        // Act
        var result = participant.Reactivate();

        // Assert
        result.IsSuccess.Should().BeTrue();
        participant.Status.Should().Be(ParticipantStatus.Active);
        participant.SuspensionReason.Should().BeNull();
    }
}
