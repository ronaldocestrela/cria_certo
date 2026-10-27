using CriaCerto.BuildingBlocks.Abstractions.Results;

namespace CriaCerto.Modules.Referral.Application.Abstractions;

/// <summary>
/// Serviço de migração e re-encriptação idempotente de dados legados no módulo Referral (LGPD Art. 46).
/// </summary>
public interface IReferralDataReencryptionService
{
    /// <summary>
    /// Varre os contratos de parceiros comerciais, garantindo que registros legados
    /// sem blind index sejam indexados e que colunas em texto puro sejam re-cifradas com AES-256-GCM.
    /// </summary>
    /// <returns>Quantidade de contratos migrados/atualizados.</returns>
    Task<Result<int>> ReencryptPartnerContractsAsync(CancellationToken cancellationToken = default);
}
