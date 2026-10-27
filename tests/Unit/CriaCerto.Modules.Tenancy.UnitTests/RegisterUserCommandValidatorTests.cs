using CriaCerto.Modules.Tenancy.Application.Features.RegisterUser;
using FluentAssertions;

namespace CriaCerto.Modules.Tenancy.UnitTests;

public class RegisterUserCommandValidatorTests
{
    private readonly RegisterUserCommandValidator _validator = new();

    [Fact]
    public void Validate_Should_Pass_When_Command_Is_Valid()
    {
        var command = new RegisterUserCommand("Carlos Eduardo", "carlos@agro.com.br", "Senha@123", AcceptTermsAndPrivacy: true);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_Should_Fail_When_AcceptTermsAndPrivacy_Is_False()
    {
        var command = new RegisterUserCommand("Carlos Eduardo", "carlos@agro.com.br", "Senha@123", AcceptTermsAndPrivacy: false);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterUserCommand.AcceptTermsAndPrivacy));
    }

    [Theory]
    [InlineData("", "carlos@agro.com.br", "Senha@123")]
    [InlineData("AB", "carlos@agro.com.br", "Senha@123")]
    [InlineData("Carlos", "email-invalido", "Senha@123")]
    [InlineData("Carlos", "carlos@agro.com.br", "123")]
    [InlineData("Carlos", "carlos@agro.com.br", "somentesemmaiustulas1")]
    public void Validate_Should_Fail_When_Inputs_Are_Invalid(string name, string email, string password)
    {
        var command = new RegisterUserCommand(name, email, password, AcceptTermsAndPrivacy: true);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
    }
}
