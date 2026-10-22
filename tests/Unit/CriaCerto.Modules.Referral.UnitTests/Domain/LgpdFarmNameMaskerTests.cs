using CriaCerto.Modules.Referral.Application.Domain.Services;
using FluentAssertions;
using Xunit;

namespace CriaCerto.Modules.Referral.UnitTests.Domain;

public class LgpdFarmNameMaskerTests
{
    [Theory]
    [InlineData("Fazenda Santa Maria", "F*****a S***a M***a")]
    [InlineData("Agropecuária Boi Gordo", "A**********a B*i G***o")]
    [InlineData("Estância do Sol", "E******a do S*l")]
    [InlineData("Rancho da Lua", "R****o da L*a")]
    [InlineData("Fazenda de Cima", "F*****a de C**a")]
    [InlineData("Boi", "B*i")]
    [InlineData("Gado", "G**o")]
    [InlineData("F", "F")]
    [InlineData("SP", "SP")]
    public void Mask_WithValidNames_ShouldApplyExample3Rule(string input, string expected)
    {
        var result = LgpdFarmNameMasker.Mask(input);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Mask_WithEmptyOrNull_ShouldReturnFallback(string? input)
    {
        var result = LgpdFarmNameMasker.Mask(input);
        result.Should().Be("Fazenda Confidencial");
    }
}
