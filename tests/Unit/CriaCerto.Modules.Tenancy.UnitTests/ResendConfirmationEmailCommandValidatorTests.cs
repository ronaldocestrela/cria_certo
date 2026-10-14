using CriaCerto.Modules.Tenancy.Application.Features.ResendConfirmationEmail;
using FluentAssertions;

namespace CriaCerto.Modules.Tenancy.UnitTests;

public class ResendConfirmationEmailCommandValidatorTests
{
    private readonly ResendConfirmationEmailCommandValidator _validator = new();

    [Fact]
    public void Validate_Should_Pass_When_Command_Is_Valid()
    {
        var command = new ResendConfirmationEmailCommand("produtor@fazenda.com.br");
        var result = _validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("email-invalido")]
    [InlineData("usuario@")]
    public void Validate_Should_Fail_When_Inputs_Are_Invalid(string email)
    {
        var command = new ResendConfirmationEmailCommand(email);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
    }
}
