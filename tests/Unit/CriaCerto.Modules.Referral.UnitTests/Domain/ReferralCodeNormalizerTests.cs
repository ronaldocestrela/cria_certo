using CriaCerto.Modules.Referral.Application.Domain.Services;
using FluentAssertions;
using Xunit;

namespace CriaCerto.Modules.Referral.UnitTests.Domain;

public class ReferralCodeNormalizerTests
{
    [Theory]
    [InlineData("Dr. João Silva (Veterinário)", "DR-JOAO-SILVA-VETERINARIO")]
    [InlineData("Consultoria Agropecuária & Zootécnica São José", "CONSULTORIA-AGROPECUARIA-ZOOTECNICA-SAO-JOSE")]
    [InlineData("fazenda modelo 2026!", "FAZENDA-MODELO-2026")]
    [InlineData("  Espaços   múltiplos  e   traços---estranhos  ", "ESPACOS-MULTIPLOS-E-TRACOS-ESTRANHOS")]
    [InlineData("ÁÉÍÓÚ-ãõç-âêîôû", "AEIOU-AOC-AEIOU")]
    public void NormalizeSlug_ShouldRemoveAccentsAndSpecialCharacters(string input, string expected)
    {
        // Act
        var result = ReferralCodeNormalizer.NormalizeSlug(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("!@#$%^&*()")]
    public void NormalizeSlug_WhenInputHasNoAlphanumericChars_ShouldFallbackToPrefix(string? input)
    {
        // Act
        var result = ReferralCodeNormalizer.NormalizeSlug(input);

        // Assert
        result.Should().StartWith("PARC-");
        result.Length.Should().BeGreaterThanOrEqualTo(8);
    }

    [Fact]
    public void NormalizeSlug_WhenLengthExceedsLimit_ShouldTruncateCleanly()
    {
        // Arrange
        var longName = new string('A', 80);

        // Act
        var result = ReferralCodeNormalizer.NormalizeSlug(longName, maxLength: 30);

        // Assert
        result.Length.Should().BeLessOrEqualTo(30);
    }
}
