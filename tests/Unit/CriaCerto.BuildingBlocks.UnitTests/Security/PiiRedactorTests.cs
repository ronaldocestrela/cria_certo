using CriaCerto.BuildingBlocks.Abstractions.Security;
using CriaCerto.BuildingBlocks.Infrastructure.Security;
using FluentAssertions;
using Xunit;

namespace CriaCerto.BuildingBlocks.UnitTests.Security;

public class PiiRedactorTests
{
    private readonly IPiiRedactor _redactor = new PiiRedactor();

    [Theory]
    [InlineData("Authorization", "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.payload.signature", "Bearer [REDACTED]")]
    [InlineData("authorization", "Basic dXNlcjpwYXNzd29yZA==", "[REDACTED]")]
    [InlineData("Cookie", "session_id=s3cr3t_c00k13; auth=xyz", "[REDACTED]")]
    [InlineData("Set-Cookie", "refresh_token=abcdef; Secure; HttpOnly", "[REDACTED]")]
    [InlineData("X-Api-Key", "live_sk_123456789", "[REDACTED]")]
    [InlineData("Proxy-Authorization", "Basic secret", "[REDACTED]")]
    [InlineData("Content-Type", "application/json; charset=utf-8", "application/json; charset=utf-8")]
    [InlineData("Accept", "application/json", "application/json")]
    public void RedactHeader_ShouldMaskSensitiveHeadersAndPreserveSafeHeaders(string headerName, string headerValue, string expected)
    {
        // Act
        var result = _redactor.RedactHeader(headerName, headerValue);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void RedactQueryString_ShouldRedactSensitiveParameters()
    {
        // Arrange
        var queryString = "?token=secretToken123&page=1&password=myPassword&pixKey=12345678909&status=active";

        // Act
        var result = _redactor.RedactQueryString(queryString);

        // Assert
        result.Should().Contain("token=[REDACTED]");
        result.Should().Contain("password=[REDACTED]");
        result.Should().Contain("pixKey=[REDACTED]");
        result.Should().Contain("page=1");
        result.Should().Contain("status=active");
        result.Should().NotContain("secretToken123");
        result.Should().NotContain("myPassword");
        result.Should().NotContain("12345678909");
    }

    [Fact]
    public void RedactJson_ShouldRedactSensitiveKeysAndPreserveNonSensitiveKeys()
    {
        // Arrange
        var json = """
        {
            "name": "João da Silva",
            "email": "joao.silva@fazenda.com.br",
            "password": "MinhaSenhaSuperSecreta123!",
            "rawPassword": "MinhaSenhaSuperSecreta123!",
            "cpf": "12345678901",
            "cnpj": "12345678000199",
            "pixKey": "joao.silva@fazenda.com.br",
            "cardNumber": "4111111111111111",
            "cvv": "123",
            "details": {
                "secret": "super_secret_payload",
                "refreshToken": "refresh_token_value",
                "farmName": "Fazenda Esperança"
            }
        }
        """;

        // Act
        var result = _redactor.RedactJson(json);

        // Assert
        result.Should().NotContain("MinhaSenhaSuperSecreta123!");
        result.Should().NotContain("4111111111111111");
        result.Should().NotContain("super_secret_payload");
        result.Should().NotContain("refresh_token_value");
        result.Should().Contain("\"farmName\":\"Fazenda Esperança\"");
        result.Should().Contain("\"password\":\"[REDACTED]\"");
        result.Should().Contain("\"rawPassword\":\"[REDACTED]\"");
        result.Should().Contain("\"pixKey\":\"[REDACTED]\"");
        result.Should().Contain("\"cvv\":\"[REDACTED]\"");
        // CPF mascarado no formato LGPD
        result.Should().Contain("***.456.789-**");
        // CNPJ mascarado no formato LGPD
        result.Should().Contain("12.***.***/0001-**");
    }

    [Fact]
    public void RedactJson_ShouldHandleInvalidJsonGracefully()
    {
        // Arrange
        var invalidJson = "This is not a JSON { text";

        // Act
        var result = _redactor.RedactJson(invalidJson);

        // Assert
        result.Should().Be(invalidJson);
    }

    [Fact]
    public void SanitizeExceptionMessage_ShouldScrubConnectionStringAndSensitiveSubstrings()
    {
        // Arrange
        var exceptionMsg = "Database error: Cannot open connection to Server=tcp:sql.cria.com.br;Database=prod;User Id=dbadmin;Password=UltraSecretPass#2026;TrustServerCertificate=True";

        // Act
        var result = _redactor.SanitizeExceptionMessage(exceptionMsg);

        // Assert
        result.Should().NotContain("UltraSecretPass#2026");
        result.Should().Contain("Password=[REDACTED]");
    }

    [Theory]
    [InlineData("/api/v1/auth/login", true)]
    [InlineData("/api/v1/backoffice/auth/login", true)]
    [InlineData("/api/v1/auth/change-password", true)]
    [InlineData("/api/v1/auth/reset-password", true)]
    [InlineData("/api/v1/auth/account/delete", true)]
    [InlineData("/api/v1/backoffice/referral/payouts", true)]
    [InlineData("/health", false)]
    [InlineData("/api/v1/backoffice/dashboard/kpis", false)]
    [InlineData("/api/v1/breeding/plantel", false)]
    public void IsSensitiveEndpoint_ShouldDetectRoutesWithConfidentialPayloads(string path, bool expected)
    {
        // Act
        var result = _redactor.IsSensitiveEndpoint(path);

        // Assert
        result.Should().Be(expected);
    }
}
