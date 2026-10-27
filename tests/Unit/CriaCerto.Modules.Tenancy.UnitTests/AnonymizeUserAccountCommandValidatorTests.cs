using CriaCerto.Modules.Tenancy.Application.Features.AnonymizeUserAccount;
using FluentAssertions;

namespace CriaCerto.Modules.Tenancy.UnitTests;

public class AnonymizeUserAccountCommandValidatorTests
{
    private readonly AnonymizeUserAccountCommandValidator _validator;

    public AnonymizeUserAccountCommandValidatorTests()
    {
        _validator = new AnonymizeUserAccountCommandValidator();
    }

    [Fact]
    public void Validate_Should_Succeed_When_All_Fields_Valid()
    {
        // Arrange
        var command = new AnonymizeUserAccountCommand(
            UserId: Guid.NewGuid(),
            CurrentPassword: "CurrentPassword123!",
            ConfirmationText: "EXCLUIR",
            ExitReason: "Motivo legítimo de saída."
        );

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_Should_Accept_Case_Insensitive_Confirmation_With_Spaces()
    {
        // Arrange
        var command = new AnonymizeUserAccountCommand(
            UserId: Guid.NewGuid(),
            CurrentPassword: "CurrentPassword123!",
            ConfirmationText: "  excluir  "
        );

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("DELETE")]
    [InlineData("CONFIRMAR")]
    [InlineData("EXCLUSAO")]
    public void Validate_Should_Fail_When_ConfirmationText_Is_Not_Excluir(string confirmationText)
    {
        // Arrange
        var command = new AnonymizeUserAccountCommand(
            UserId: Guid.NewGuid(),
            CurrentPassword: "CurrentPassword123!",
            ConfirmationText: confirmationText
        );

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(AnonymizeUserAccountCommand.ConfirmationText));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Validate_Should_Fail_When_CurrentPassword_Is_Empty(string? password)
    {
        // Arrange
        var command = new AnonymizeUserAccountCommand(
            UserId: Guid.NewGuid(),
            CurrentPassword: password!,
            ConfirmationText: "EXCLUIR"
        );

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(AnonymizeUserAccountCommand.CurrentPassword));
    }

    [Fact]
    public void Validate_Should_Fail_When_UserId_Is_Empty()
    {
        // Arrange
        var command = new AnonymizeUserAccountCommand(
            UserId: Guid.Empty,
            CurrentPassword: "Password123!",
            ConfirmationText: "EXCLUIR"
        );

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(AnonymizeUserAccountCommand.UserId));
    }

    [Fact]
    public void Validate_Should_Fail_When_ExitReason_Exceeds_500_Chars()
    {
        // Arrange
        var command = new AnonymizeUserAccountCommand(
            UserId: Guid.NewGuid(),
            CurrentPassword: "Password123!",
            ConfirmationText: "EXCLUIR",
            ExitReason: new string('A', 501)
        );

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(AnonymizeUserAccountCommand.ExitReason));
    }
}
