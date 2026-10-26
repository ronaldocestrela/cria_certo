using CriaCerto.Modules.Tenancy.Application.Features.UpdateUserProfile;
using FluentAssertions;

namespace CriaCerto.Modules.Tenancy.UnitTests;

public class UpdateUserProfileCommandValidatorTests
{
    private readonly UpdateUserProfileCommandValidator _validator = new();

    [Fact]
    public void Validate_Should_Pass_For_Valid_Command()
    {
        var command = new UpdateUserProfileCommand(
            Guid.NewGuid(),
            "Lucas Mendes",
            "+55 67 98888-7777");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Ab")]
    public void Validate_Should_Fail_For_Invalid_FullName(string invalidName)
    {
        var command = new UpdateUserProfileCommand(
            Guid.NewGuid(),
            invalidName,
            "+55 67 98888-7777");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateUserProfileCommand.FullName));
    }

    [Fact]
    public void Validate_Should_Pass_When_PhoneNumber_Is_Null_Or_Empty()
    {
        var command = new UpdateUserProfileCommand(
            Guid.NewGuid(),
            "Lucas Mendes",
            null);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("123")] // Too short
    [InlineData("123456789012345678")] // Too long (> 15 digits)
    public void Validate_Should_Fail_For_Invalid_PhoneNumber_Digits(string invalidPhone)
    {
        var command = new UpdateUserProfileCommand(
            Guid.NewGuid(),
            "Lucas Mendes",
            invalidPhone);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateUserProfileCommand.PhoneNumber));
    }
}
