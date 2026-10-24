using System.Text;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Features.Payouts.Dtos;
using CriaCerto.Modules.Referral.Application.Features.Payouts.Services;
using FluentAssertions;
using Xunit;

namespace CriaCerto.Modules.Referral.UnitTests.Services;

public class PixBatchCsvGeneratorTests
{
    private readonly PixBatchCsvGenerator _generator = new();

    [Fact]
    public void GenerateCsv_WithEmptyList_ShouldReturnOnlyHeader()
    {
        // Act
        var bytes = _generator.GenerateCsv(Array.Empty<AvailablePartnerPayoutDto>());
        var content = Encoding.UTF8.GetString(bytes);

        // Assert
        content.Should().Contain("NOME_FAVORECIDO");
        content.Should().Contain("CHAVE_PIX");
        content.Should().Contain("VALOR_LIQUIDO");
    }

    [Fact]
    public void GenerateCsv_WithPayouts_ShouldGenerateValidCsvRows()
    {
        // Arrange
        var partnerId = Guid.NewGuid();
        var payout = new AvailablePartnerPayoutDto(
            partnerId,
            PartnerName: "Veterinária Campo Verde LTDA",
            TradeName: "Campo Verde",
            LegalDocumentNumber: "12.345.678/0001-90",
            PixKey: "financeiro@campoverde.com.br",
            PixKeyType: PixKeyType.Email,
            TotalAvailableAmount: 1250.50m,
            CommissionsCount: 3,
            Commissions: Array.Empty<CommissionDetailDto>()
        );

        // Act
        var bytes = _generator.GenerateCsv(new[] { payout });
        var content = Encoding.UTF8.GetString(bytes);

        // Assert
        content.Should().Contain("Veterinária Campo Verde LTDA");
        content.Should().Contain("financeiro@campoverde.com.br");
        content.Should().Contain("Email");
        content.Should().Contain("1250.50");
        content.Should().Contain(partnerId.ToString());
    }
}
