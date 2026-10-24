using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Domain;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Domain.Errors;
using FluentAssertions;
using Xunit;

namespace CriaCerto.Modules.Referral.UnitTests.Domain;

public class CommercialPartnerProfileTests
{
    [Fact]
    public void Create_WithValidParameters_ShouldSucceed()
    {
        // Arrange
        var participantId = Guid.NewGuid();
        var fullName = "Dr. Roberto Antunes da Silva";
        var tradeName = "Consultoria Zootécnica Antunes";
        var email = "roberto@antuneszootecnia.com.br";
        var phone = "+5567999887766";
        var category = PartnerCategory.ZootechnicianConsultant;
        var city = "Campo Grande";
        var state = "MS";
        var notes = "Parceiro consultor sênior em IATF";

        // Act
        var result = CommercialPartnerProfile.Create(
            participantId,
            fullName,
            tradeName,
            email,
            phone,
            category,
            city,
            state,
            notes);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ParticipantId.Should().Be(participantId);
        result.Value.FullName.Should().Be(fullName);
        result.Value.TradeName.Should().Be(tradeName);
        result.Value.Email.Should().Be("roberto@antuneszootecnia.com.br");
        result.Value.PhoneNumber.Should().Be(phone);
        result.Value.Category.Should().Be(PartnerCategory.ZootechnicianConsultant);
        result.Value.City.Should().Be("Campo Grande");
        result.Value.State.Should().Be("MS");
        result.Value.Notes.Should().Be(notes);
        result.Value.CreatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithoutFullName_ShouldFail(string? invalidFullName)
    {
        // Act
        var result = CommercialPartnerProfile.Create(
            Guid.NewGuid(),
            invalidFullName!,
            "Trade",
            "test@partner.com",
            "+5511999998888",
            PartnerCategory.Veterinarian,
            "Cuiabá",
            "MT");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("CommercialPartnerProfile.FullNameRequired");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithoutEmail_ShouldFail(string? invalidEmail)
    {
        // Act
        var result = CommercialPartnerProfile.Create(
            Guid.NewGuid(),
            "Dr. Carlos",
            null,
            invalidEmail!,
            "+5511999998888",
            PartnerCategory.Veterinarian,
            "Goiânia",
            "GO");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("CommercialPartnerProfile.EmailRequired");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithoutPhone_ShouldFail(string? invalidPhone)
    {
        // Act
        var result = CommercialPartnerProfile.Create(
            Guid.NewGuid(),
            "Dr. Carlos",
            null,
            "carlos@vet.com",
            invalidPhone!,
            PartnerCategory.Veterinarian,
            "Goiânia",
            "GO");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("CommercialPartnerProfile.PhoneRequired");
    }

    [Fact]
    public void Create_WithEmptyParticipantId_ShouldFail()
    {
        // Act
        var result = CommercialPartnerProfile.Create(
            Guid.Empty,
            "Dr. Carlos",
            null,
            "carlos@vet.com",
            "+5511999998888",
            PartnerCategory.Veterinarian,
            "Goiânia",
            "GO");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("CommercialPartnerProfile.ParticipantRequired");
    }

    [Fact]
    public void UpdateContactInfo_WithValidData_ShouldUpdateProperties()
    {
        // Arrange
        var profile = CommercialPartnerProfile.Create(
            Guid.NewGuid(),
            "Dr. Carlos",
            "Carlos Vet",
            "carlos@vet.com",
            "+5511999998888",
            PartnerCategory.Veterinarian,
            "Goiânia",
            "GO").Value;

        // Act
        var result = profile.UpdateContactInfo(
            "Carlos Eduardo Silva",
            "Silva Reprodução Animal",
            "carlos.silva@vet.com",
            "+5511988887777",
            PartnerCategory.ZootechnicianConsultant,
            "Rio Verde",
            "GO",
            "Atualizado contato comercial");

        // Assert
        result.IsSuccess.Should().BeTrue();
        profile.FullName.Should().Be("Carlos Eduardo Silva");
        profile.TradeName.Should().Be("Silva Reprodução Animal");
        profile.Email.Should().Be("carlos.silva@vet.com");
        profile.PhoneNumber.Should().Be("+5511988887777");
        profile.Category.Should().Be(PartnerCategory.ZootechnicianConsultant);
        profile.City.Should().Be("Rio Verde");
        profile.State.Should().Be("GO");
        profile.Notes.Should().Be("Atualizado contato comercial");
        profile.UpdatedAtUtc.Should().NotBeNull();
    }
}
