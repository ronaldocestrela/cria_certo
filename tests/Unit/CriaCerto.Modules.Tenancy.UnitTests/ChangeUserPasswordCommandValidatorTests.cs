using CriaCerto.Modules.Tenancy.Application.Features.ChangeUserPassword;
using FluentAssertions;

namespace CriaCerto.Modules.Tenancy.UnitTests;

public class ChangeUserPasswordCommandValidatorTests
{
    private readonly ChangeUserPasswordCommandValidator _validator = new();

    [Fact]
    public void Validate_Should_Pass_For_Valid_Strong_Password()
    {
        var command = new ChangeUserPasswordCommand(
            Guid.NewGuid(),
            "SenhaAtual@123",
            "NovaSenha@Forte2026",
            "NovaSenha@Forte2026");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("short1!")] // < 8 chars
    [InlineData("nouppercase123!")] // no uppercase
    [InlineData("NOLOWERCASE123!")] // no lowercase
    [InlineData("NoNumberSymbol!")] // no number
    [InlineData("NoSymbol1234567")] // no symbol
    public void Validate_Should_Fail_When_NewPassword_Does_Not_Meet_Complexity(string weakPassword)
    {
        var command = new ChangeUserPasswordCommand(
            Guid.NewGuid(),
            "SenhaAtual@123",
            weakPassword,
            weakPassword);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ChangeUserPasswordCommand.NewPassword));
    }

    [Fact]
    public void Validate_Should_Fail_When_ConfirmNewPassword_Does_Not_Match()
    {
        var command = new ChangeUserPasswordCommand(
            Guid.NewGuid(),
            "SenhaAtual@123",
            "NovaSenha@Forte2026",
            "OutraSenhaDiferente#999");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ChangeUserPasswordCommand.ConfirmNewPassword));
    }
}
