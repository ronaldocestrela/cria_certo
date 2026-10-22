using System.Globalization;
using System.Text;
using CriaCerto.Modules.Referral.Application.Features.Payouts.Dtos;

namespace CriaCerto.Modules.Referral.Application.Features.Payouts.Services;

public sealed class PixBatchCsvGenerator : IPixBatchCsvGenerator
{
    private static readonly Encoding Utf8WithBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    public byte[] GenerateCsv(IReadOnlyList<AvailablePartnerPayoutDto> payouts)
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine("ID_PARTICIPANTE;NOME_FAVORECIDO;NOME_FANTASIA;DOCUMENTO_CPF_CNPJ;TIPO_CHAVE_PIX;CHAVE_PIX;VALOR_LIQUIDO;QUANTIDADE_LANCAMENTOS;IDENTIFICADOR_REMESSA");

        var batchTimestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);

        foreach (var p in payouts)
        {
            var id = p.ParticipantId.ToString();
            var name = EscapeCsv(p.PartnerName);
            var tradeName = EscapeCsv(p.TradeName ?? string.Empty);
            var doc = EscapeCsv(p.LegalDocumentNumber);
            var keyType = p.PixKeyType.ToString();
            var key = EscapeCsv(p.PixKey);
            var amount = p.TotalAvailableAmount.ToString("F2", CultureInfo.InvariantCulture);
            var count = p.CommissionsCount.ToString(CultureInfo.InvariantCulture);
            var refId = $"PIX-{batchTimestamp}-{id[..8].ToUpperInvariant()}";

            sb.AppendLine($"{id};{name};{tradeName};{doc};{keyType};{key};{amount};{count};{refId}");
        }

        return Utf8WithBom.GetBytes(sb.ToString());
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        var escaped = value.Replace("\"", "\"\"");
        if (escaped.Contains(';') || escaped.Contains('\n') || escaped.Contains('\r') || escaped.Contains('"'))
        {
            return $"\"{escaped}\"";
        }

        return escaped;
    }
}
