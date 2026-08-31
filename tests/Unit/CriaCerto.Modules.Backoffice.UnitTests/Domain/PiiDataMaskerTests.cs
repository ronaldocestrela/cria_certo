using CriaCerto.Modules.Backoffice.Application.Domain.Services;
using FluentAssertions;
using Xunit;

namespace CriaCerto.Modules.Backoffice.UnitTests.Domain;

public class PiiDataMaskerTests
{
    private readonly IPiiDataMasker _masker = new PiiDataMasker();

    [Theory]
    [InlineData("123.456.789-00", "***.456.789-**")]
    [InlineData("12345678900", "***.456.789-**")]
    [InlineData("98765432109", "***.654.321-**")]
    public void MaskCpf_WhenValidCpfProvided_ShouldMaskCorrectly(string input, string expected)
    {
        // Act
        var result = _masker.MaskCpf(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("123", "***")]
    public void MaskCpf_WhenNullOrEmptyOrShort_ShouldHandleSafely(string? input, string expected)
    {
        // Act
        var result = _masker.MaskCpf(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("12.345.678/0001-90", "12.***.***/0001-**")]
    [InlineData("12345678000190", "12.***.***/0001-**")]
    [InlineData("98765432000211", "98.***.***/0002-**")]
    public void MaskCnpj_WhenValidCnpjProvided_ShouldMaskMiddleDigitsAndCheckDigits(string input, string expected)
    {
        // Act
        var result = _masker.MaskCnpj(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("123.456.789-00", "***.456.789-**")]
    [InlineData("12345678900", "***.456.789-**")]
    [InlineData("12.345.678/0001-90", "12.***.***/0001-**")]
    [InlineData("12345678000190", "12.***.***/0001-**")]
    public void MaskDocument_ShouldDetectCpfVsCnpjAndMaskAppropriately(string input, string expected)
    {
        // Act
        var result = _masker.MaskDocument(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("ronaldo.estrela@criacerto.com.br", "r***a@criacerto.com.br")]
    [InlineData("contato@fazenda.com.br", "c***o@fazenda.com.br")]
    [InlineData("a@b.com", "a***@b.com")]
    [InlineData("ab@teste.com", "a***b@teste.com")]
    public void MaskEmail_WhenValidEmailProvided_ShouldPreserveDomainAndMaskLocalPart(string input, string expected)
    {
        // Act
        var result = _masker.MaskEmail(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("(11) 98765-4321", "(11) 9****-**21")]
    [InlineData("11987654321", "(11) 9****-**21")]
    [InlineData("(67) 3344-5566", "(67) 3****-**66")]
    public void MaskPhone_WhenValidPhoneProvided_ShouldMaskMiddleDigits(string input, string expected)
    {
        // Act
        var result = _masker.MaskPhone(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("192.168.1.100", "192.168.***.***")]
    [InlineData("177.136.241.10", "177.136.***.***")]
    [InlineData("10.0.0.1", "10.0.***.***")]
    [InlineData("2804:14d:5481:8150:1234:5678:9abc:def0", "2804:14d:***")]
    [InlineData("::1", "::1")]
    public void MaskIpAddress_WhenIpProvided_ShouldMaskSubnetOctets(string input, string expected)
    {
        // Act
        var result = _masker.MaskIpAddress(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("Ronaldo Costa Estrela", "Ronaldo C. E.")]
    [InlineData("Carlos Eduardo dos Santos Silva", "Carlos E. d. S. S.")]
    [InlineData("Admin", "Admin")]
    public void MaskPersonName_WhenFullNameProvided_ShouldAbbreviateSurnames(string input, string expected)
    {
        // Act
        var result = _masker.MaskPersonName(input);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void SanitizeJsonDetails_WhenContainsSensitiveKeys_ShouldMaskValues()
    {
        // Arrange
        var rawJson = "{\"password\":\"Secret123!\",\"cpf\":\"12345678900\",\"email\":\"ronaldo@fazenda.com.br\",\"normalKey\":\"valorNormal\"}";

        // Act
        var sanitized = _masker.SanitizeJsonDetails(rawJson);

        // Assert
        sanitized.Should().NotContain("Secret123!");
        sanitized.Should().Contain("\"password\":\"***\"");
        sanitized.Should().Contain("***.456.789-**");
        sanitized.Should().Contain("valorNormal");
    }
}
