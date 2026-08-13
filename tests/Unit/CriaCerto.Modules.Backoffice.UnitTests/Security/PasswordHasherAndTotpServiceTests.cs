using CriaCerto.Modules.Backoffice.Infrastructure.Security;
using FluentAssertions;
using Xunit;

namespace CriaCerto.Modules.Backoffice.UnitTests.Security;

public class PasswordHasherAndTotpServiceTests
{
    [Fact]
    public void HashPassword_And_VerifyPassword_ShouldBeValid()
    {
        // Arrange
        var hasher = new PasswordHasherService();
        var password = "SuperSecretPassword123!";

        // Act
        var hash = hasher.HashPassword(password);
        var isValid = hasher.VerifyPassword(password, hash);
        var isInvalid = hasher.VerifyPassword("WrongPassword", hash);

        // Assert
        hash.Should().NotBeNullOrWhiteSpace();
        hash.Should().Contain(".");
        isValid.Should().BeTrue();
        isInvalid.Should().BeFalse();
    }

    [Fact]
    public void TotpService_GenerateSecret_And_QrCodeUri_ShouldBeValidFormat()
    {
        // Arrange
        var totp = new TotpService();
        var email = "admin@criacerto.com.br";

        // Act
        var secret = totp.GenerateSecretKey();
        var qrUri = totp.GenerateQrCodeUri(email, secret);
        var recoveryCodes = totp.GenerateRecoveryCodes(4);

        // Assert
        secret.Should().NotBeNullOrWhiteSpace();
        secret.Length.Should().BeGreaterThanOrEqualTo(16);

        qrUri.Should().StartWith("otpauth://totp/");
        qrUri.Should().Contain(secret);

        recoveryCodes.Should().HaveCount(4);
    }
}
