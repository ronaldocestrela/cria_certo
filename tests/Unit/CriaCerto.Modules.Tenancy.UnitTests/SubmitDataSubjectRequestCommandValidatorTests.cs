using CriaCerto.Modules.Tenancy.Application.Features.Compliance.Dpo;
using CriaCerto.Modules.Tenancy.Application.Features.Compliance.Dpo.Models;
using FluentAssertions;

namespace CriaCerto.Modules.Tenancy.UnitTests;

public class SubmitDataSubjectRequestCommandValidatorTests
{
    private readonly SubmitDataSubjectRequestCommandValidator _validator;

    public SubmitDataSubjectRequestCommandValidatorTests()
    {
        _validator = new SubmitDataSubjectRequestCommandValidator();
    }

    [Fact]
    public void Validate_Should_Succeed_When_All_Fields_Valid()
    {
        // Arrange
        var command = new SubmitDataSubjectRequestCommand(
            FullName: "João da Silva Sauro",
            Email: "joao.sauro@fazendasaopedro.com.br",
            PhoneNumber: "+55 (65) 99988-7766",
            DocumentNumber: "123.456.789-00",
            RelationshipType: DataSubjectRole.ProducerAdmin,
            RequestType: DataSubjectRequestType.AccessAndConfirmation,
            Description: "Solicito confirmação da existência de tratamento dos meus dados e histórico de acesso."
        );

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Ab")]
    public void Validate_Should_Fail_When_FullName_Is_Empty_Or_Too_Short(string fullName)
    {
        // Arrange
        var command = new SubmitDataSubjectRequestCommand(
            FullName: fullName,
            Email: "joao.sauro@fazendasaopedro.com.br",
            PhoneNumber: null,
            DocumentNumber: null,
            RelationshipType: DataSubjectRole.ProducerAdmin,
            RequestType: DataSubjectRequestType.AccessAndConfirmation,
            Description: "Solicito confirmação da existência de tratamento dos meus dados pessoais."
        );

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(SubmitDataSubjectRequestCommand.FullName));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("teste@")]
    public void Validate_Should_Fail_When_Email_Is_Invalid(string email)
    {
        // Arrange
        var command = new SubmitDataSubjectRequestCommand(
            FullName: "João da Silva Sauro",
            Email: email,
            PhoneNumber: null,
            DocumentNumber: null,
            RelationshipType: DataSubjectRole.ProducerAdmin,
            RequestType: DataSubjectRequestType.AccessAndConfirmation,
            Description: "Solicito confirmação da existência de tratamento dos meus dados pessoais."
        );

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(SubmitDataSubjectRequestCommand.Email));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Curto")]
    [InlineData("Menos de 20 chars.")]
    public void Validate_Should_Fail_When_Description_Has_Less_Than_20_Characters(string description)
    {
        // Arrange
        var command = new SubmitDataSubjectRequestCommand(
            FullName: "João da Silva Sauro",
            Email: "joao.sauro@fazendasaopedro.com.br",
            PhoneNumber: null,
            DocumentNumber: null,
            RelationshipType: DataSubjectRole.ProducerAdmin,
            RequestType: DataSubjectRequestType.AccessAndConfirmation,
            Description: description
        );

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(SubmitDataSubjectRequestCommand.Description));
    }

    [Fact]
    public void Validate_Should_Fail_When_RelationshipType_Is_Invalid_Enum()
    {
        // Arrange
        var command = new SubmitDataSubjectRequestCommand(
            FullName: "João da Silva Sauro",
            Email: "joao.sauro@fazendasaopedro.com.br",
            PhoneNumber: null,
            DocumentNumber: null,
            RelationshipType: (DataSubjectRole)999,
            RequestType: DataSubjectRequestType.AccessAndConfirmation,
            Description: "Solicito confirmação da existência de tratamento dos meus dados pessoais."
        );

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(SubmitDataSubjectRequestCommand.RelationshipType));
    }

    [Fact]
    public void Validate_Should_Fail_When_RequestType_Is_Invalid_Enum()
    {
        // Arrange
        var command = new SubmitDataSubjectRequestCommand(
            FullName: "João da Silva Sauro",
            Email: "joao.sauro@fazendasaopedro.com.br",
            PhoneNumber: null,
            DocumentNumber: null,
            RelationshipType: DataSubjectRole.ProducerAdmin,
            RequestType: (DataSubjectRequestType)999,
            Description: "Solicito confirmação da existência de tratamento dos meus dados pessoais."
        );

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(SubmitDataSubjectRequestCommand.RequestType));
    }
}
