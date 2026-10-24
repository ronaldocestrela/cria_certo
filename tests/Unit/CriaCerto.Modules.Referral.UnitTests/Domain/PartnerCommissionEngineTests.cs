using CriaCerto.Modules.Referral.Application.Domain;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace CriaCerto.Modules.Referral.UnitTests.Domain;

/// <summary>
/// Roadmap 6.2 Testes Automatizados: Motor de cálculo de comissão com diferentes regras contratuais de parceiro.
/// </summary>
public class PartnerCommissionEngineTests
{
    [Theory]
    [InlineData(100, 20, 20)]         // 20% de R$ 100 no 1º mês = R$ 20
    [InlineData(450, 30, 135)]        // 30% de R$ 450 no 1º mês = R$ 135
    [InlineData(1250.50, 15, 187.58)] // 15% de R$ 1250,50 no 1º mês = R$ 187,58 (arredondado)
    public void CalculateCommission_PercentageFirstMonth_FirstMonth_ShouldCalculateAccurately(
        decimal invoiceAmount, decimal rate, decimal expectedCommission)
    {
        // Arrange
        var contract = PartnerContract.Create(
            Guid.NewGuid(),
            CommissionType.PercentageFirstMonth,
            commissionValue: rate,
            maxRecurringMonths: 1,
            paymentTermsDays: 30,
            pixKey: "consultor@agri.com.br",
            pixKeyType: PixKeyType.Email,
            legalDocumentNumber: "12.345.678/0001-90",
            validFromUtc: DateTime.UtcNow).Value;

        // Act
        var commission = contract.CalculateCommission(invoiceAmount, paymentCycleNumber: 1);

        // Assert
        commission.Should().Be(expectedCommission);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(12)]
    public void CalculateCommission_PercentageFirstMonth_SubsequentMonths_ShouldReturnZero(int cycleNumber)
    {
        // Arrange
        var contract = PartnerContract.Create(
            Guid.NewGuid(),
            CommissionType.PercentageFirstMonth,
            commissionValue: 25m,
            maxRecurringMonths: 1,
            paymentTermsDays: 30,
            pixKey: "consultor@agri.com.br",
            pixKeyType: PixKeyType.Email,
            legalDocumentNumber: "12.345.678/0001-90",
            validFromUtc: DateTime.UtcNow).Value;

        // Act
        var commission = contract.CalculateCommission(invoiceAmount: 500m, paymentCycleNumber: cycleNumber);

        // Assert: Apenas o 1º mês recebe comissão; meses subsequentes devem ser zero
        commission.Should().Be(0m);
    }

    [Theory]
    [InlineData(1, 100)]   // Mês 1: 20% de R$ 500 = R$ 100
    [InlineData(6, 100)]   // Mês 6: 20% de R$ 500 = R$ 100
    [InlineData(12, 100)]  // Mês 12: 20% de R$ 500 = R$ 100
    public void CalculateCommission_PercentageRecurring_WithinMaxMonths_ShouldPayCommission(
        int cycleNumber, decimal expectedCommission)
    {
        // Arrange: Contrato recorrente de 20% por até 12 meses
        var contract = PartnerContract.Create(
            Guid.NewGuid(),
            CommissionType.PercentageRecurring,
            commissionValue: 20m,
            maxRecurringMonths: 12,
            paymentTermsDays: 30,
            pixKey: "12345678909",
            pixKeyType: PixKeyType.Cpf,
            legalDocumentNumber: "123.456.789-09",
            validFromUtc: DateTime.UtcNow).Value;

        // Act
        var commission = contract.CalculateCommission(invoiceAmount: 500m, paymentCycleNumber: cycleNumber);

        // Assert
        commission.Should().Be(expectedCommission);
    }

    [Theory]
    [InlineData(13)]
    [InlineData(24)]
    public void CalculateCommission_PercentageRecurring_ExceedingMaxMonths_ShouldReturnZero(int cycleNumber)
    {
        // Arrange: Contrato recorrente limitado a 12 meses
        var contract = PartnerContract.Create(
            Guid.NewGuid(),
            CommissionType.PercentageRecurring,
            commissionValue: 20m,
            maxRecurringMonths: 12,
            paymentTermsDays: 30,
            pixKey: "12345678909",
            pixKeyType: PixKeyType.Cpf,
            legalDocumentNumber: "123.456.789-09",
            validFromUtc: DateTime.UtcNow).Value;

        // Act
        var commission = contract.CalculateCommission(invoiceAmount: 500m, paymentCycleNumber: cycleNumber);

        // Assert: Ciclos após o 12º mês não recebem mais repasse
        commission.Should().Be(0m);
    }

    [Fact]
    public void CalculateCommission_PercentageRecurring_WithoutMonthLimit_ShouldPayContinuously()
    {
        // Arrange: Contrato vitalício/sem limite de meses (MaxRecurringMonths = null)
        var contract = PartnerContract.Create(
            Guid.NewGuid(),
            CommissionType.PercentageRecurring,
            commissionValue: 15m,
            maxRecurringMonths: null,
            paymentTermsDays: 30,
            pixKey: "12345678909",
            pixKeyType: PixKeyType.Cpf,
            legalDocumentNumber: "123.456.789-09",
            validFromUtc: DateTime.UtcNow).Value;

        // Act & Assert
        contract.CalculateCommission(invoiceAmount: 600m, paymentCycleNumber: 1).Should().Be(90m);
        contract.CalculateCommission(invoiceAmount: 600m, paymentCycleNumber: 15).Should().Be(90m);
        contract.CalculateCommission(invoiceAmount: 600m, paymentCycleNumber: 36).Should().Be(90m);
    }

    [Fact]
    public void CalculateCommission_FixedPerConversion_FirstCycle_ShouldReturnFixedValue()
    {
        // Arrange: R$ 250 fixo por conversão
        var contract = PartnerContract.Create(
            Guid.NewGuid(),
            CommissionType.FixedPerConversion,
            commissionValue: 250m,
            maxRecurringMonths: null,
            paymentTermsDays: 30,
            pixKey: "12345678909",
            pixKeyType: PixKeyType.Cpf,
            legalDocumentNumber: "123.456.789-09",
            validFromUtc: DateTime.UtcNow).Value;

        // Act
        var commission = contract.CalculateCommission(invoiceAmount: 1200m, paymentCycleNumber: 1);

        // Assert: Recebe exatamente o fixo
        commission.Should().Be(250m);
    }

    [Fact]
    public void CalculateCommission_FixedPerConversion_SubsequentCycles_ShouldReturnZero()
    {
        // Arrange: Fixo por conversão
        var contract = PartnerContract.Create(
            Guid.NewGuid(),
            CommissionType.FixedPerConversion,
            commissionValue: 250m,
            maxRecurringMonths: null,
            paymentTermsDays: 30,
            pixKey: "12345678909",
            pixKeyType: PixKeyType.Cpf,
            legalDocumentNumber: "123.456.789-09",
            validFromUtc: DateTime.UtcNow).Value;

        // Act
        var commissionCycle2 = contract.CalculateCommission(invoiceAmount: 1200m, paymentCycleNumber: 2);

        // Assert: Renovações não pagam novamente comissão fixa de conversão
        commissionCycle2.Should().Be(0m);
    }

    [Fact]
    public void CalculateCommission_WhenContractIsClosed_ShouldReturnZero()
    {
        // Arrange
        var contract = PartnerContract.Create(
            Guid.NewGuid(),
            CommissionType.PercentageRecurring,
            commissionValue: 20m,
            maxRecurringMonths: 12,
            paymentTermsDays: 30,
            pixKey: "12345678909",
            pixKeyType: PixKeyType.Cpf,
            legalDocumentNumber: "123.456.789-09",
            validFromUtc: DateTime.UtcNow.AddMonths(-2)).Value;

        contract.CloseContract(DateTime.UtcNow.AddDays(-1));

        // Act
        var commission = contract.CalculateCommission(invoiceAmount: 500m, paymentCycleNumber: 1);

        // Assert
        commission.Should().Be(0m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void CalculateCommission_WithZeroOrNegativeInvoice_ShouldReturnZero(decimal invoiceAmount)
    {
        // Arrange
        var contract = PartnerContract.Create(
            Guid.NewGuid(),
            CommissionType.PercentageRecurring,
            commissionValue: 20m,
            maxRecurringMonths: 12,
            paymentTermsDays: 30,
            pixKey: "12345678909",
            pixKeyType: PixKeyType.Cpf,
            legalDocumentNumber: "123.456.789-09",
            validFromUtc: DateTime.UtcNow).Value;

        // Act
        var commission = contract.CalculateCommission(invoiceAmount, paymentCycleNumber: 1);

        // Assert
        commission.Should().Be(0m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void CalculateCommission_WithInvalidCycleNumber_ShouldReturnZero(int cycleNumber)
    {
        // Arrange
        var contract = PartnerContract.Create(
            Guid.NewGuid(),
            CommissionType.PercentageRecurring,
            commissionValue: 20m,
            maxRecurringMonths: 12,
            paymentTermsDays: 30,
            pixKey: "12345678909",
            pixKeyType: PixKeyType.Cpf,
            legalDocumentNumber: "123.456.789-09",
            validFromUtc: DateTime.UtcNow).Value;

        // Act
        var commission = contract.CalculateCommission(500m, cycleNumber);

        // Assert
        commission.Should().Be(0m);
    }
}
