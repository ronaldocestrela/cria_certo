using CriaCerto.Modules.Breeding.Application.Domain;
using CriaCerto.Modules.Breeding.Application.Domain.Services;
using FluentAssertions;
using Xunit;

namespace CriaCerto.Modules.Breeding.UnitTests.Domain;

public class IatfProtocolTests
{
    private readonly Guid _tenantId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidParameters_ShouldSucceed()
    {
        var cowId = Guid.NewGuid();
        var result = IatfProtocol.Create("Protocolo IATF Primavera", DateTime.UtcNow, DateTime.UtcNow.AddDays(10), Guid.NewGuid(), new List<Guid> { cowId }, _tenantId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Protocolo IATF Primavera");
        result.Value.CowIds.Should().Contain(cowId);
        result.Value.BullId.Should().BeNull();
        result.Value.BullName.Should().BeNull();
    }

    [Fact]
    public void Create_WithBull_ShouldPersistBullIdAndBullName()
    {
        var cowId = Guid.NewGuid();
        var bullId = Guid.NewGuid();
        var bullName = "BR-01 - Touro Barão (Nelore)";

        var result = IatfProtocol.Create(
            "Protocolo IATF Outono",
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(10),
            Guid.NewGuid(),
            new List<Guid> { cowId },
            _tenantId,
            bullId,
            bullName);

        result.IsSuccess.Should().BeTrue();
        result.Value.BullId.Should().Be(bullId);
        result.Value.BullName.Should().Be(bullName);
    }

    [Fact]
    public void Create_WithoutBull_WhenExternalBull_ShouldHaveNullBull()
    {
        var cowId = Guid.NewGuid();

        var result = IatfProtocol.Create(
            "Protocolo IATF Semen Externo",
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(10),
            Guid.NewGuid(),
            new List<Guid> { cowId },
            _tenantId,
            bullId: null,
            bullName: null);

        result.IsSuccess.Should().BeTrue();
        result.Value.BullId.Should().BeNull();
        result.Value.BullName.Should().BeNull();
    }
}

public class IepCalculatorTests
{
    [Fact]
    public void CalculateIepMonths_WithValidDates_ShouldReturnCorrectMonths()
    {
        var prevParto = new DateTime(2024, 1, 1);
        var currentParto = new DateTime(2025, 1, 1); // 366 dias (~12.0 meses)

        var iep = IepCalculator.CalculateIepMonths(prevParto, currentParto);

        iep.Should().NotBeNull();
        iep.Should().BeInRange(11.8, 12.2);
    }

    [Fact]
    public void CalculateOpenDays_ShouldReturnDaysDifference()
    {
        var lastCalving = new DateTime(2025, 1, 1);
        var diagnosisDate = new DateTime(2025, 3, 2); // 60 dias depois

        var openDays = IepCalculator.CalculateOpenDays(lastCalving, diagnosisDate);

        openDays.Should().Be(60);
    }
}
