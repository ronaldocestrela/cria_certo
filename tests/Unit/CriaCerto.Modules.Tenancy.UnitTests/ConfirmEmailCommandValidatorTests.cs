using CriaCerto.Modules.Tenancy.Application.Features.ConfirmEmail;
using FluentAssertions;

namespace CriaCerto.Modules.Tenancy.UnitTests;

public class ConfirmEmailCommandValidatorTests
{
    private readonly ConfirmEmailCommandValidator _validator = new();

    [Fact]
    public void Validate_Should_Pass_When_Command_Is_Valid()
    {
        var command = new ConfirmEmailCommand("produtor@fazenda.com.br", "token123");
        var result = _validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "token123")]
    [InlineData("email-invalido", "token123")]
    [InlineData("produtor@fazenda.com.br", "")]
    [InlineData("produtor@fazenda.com.br", "   ")]
    public void Validate_Should_Fail_When_Inputs_Are_Invalid(string email, string token)
    {
        var command = new ConfirmEmailCommand(email, token);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
    }
}
