using CriaCerto.Modules.Tenancy.Application.Domain;
using FluentAssertions;

namespace CriaCerto.Modules.Tenancy.UnitTests;

public class CnpjNormalizerTests
{
    [Theory]
    [InlineData("12.345.678/0001-90", "12345678000190")]
    [InlineData("123.456.789-09", "12345678909")]
    [InlineData("", "")]
    public void Normalize_Should_Extract_Digits(string input, string expected)
    {
        CnpjNormalizer.Normalize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("12.345.678/0001-90", true)]
    [InlineData("123.456.789-09", true)]
    [InlineData("123", false)]
    [InlineData("", false)]
    public void IsValidCnpjOrCpf_Should_Validate_Length(string input, bool expected)
    {
        CnpjNormalizer.IsValidCnpjOrCpf(input).Should().Be(expected);
    }
}

public class PlanCapacityLimitsTests
{
    [Theory]
    [InlineData("Starter", 500, true)]
    [InlineData("Starter", 501, false)]
    [InlineData("Pro", 2500, true)]
    [InlineData("Enterprise", int.MaxValue, true)]
    public void IsCapacityWithinPlan_Should_Use_Catalog_Limits(string plan, int capacity, bool expected)
    {
        PlanCapacityLimits.IsCapacityWithinPlan(plan, capacity).Should().Be(expected);
    }
}
