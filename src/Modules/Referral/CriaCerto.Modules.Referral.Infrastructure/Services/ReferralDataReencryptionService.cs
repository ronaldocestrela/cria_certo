using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.BuildingBlocks.Abstractions.Security;
using CriaCerto.Modules.Referral.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CriaCerto.Modules.Referral.Infrastructure.Services;

public sealed class ReferralDataReencryptionService : IReferralDataReencryptionService
{
    private readonly IReferralDbContext _dbContext;
    private readonly IDataEncryptionService _encryptionService;
    private readonly ILogger<ReferralDataReencryptionService> _logger;

    public ReferralDataReencryptionService(
        IReferralDbContext dbContext,
        IDataEncryptionService encryptionService,
        ILogger<ReferralDataReencryptionService> logger)
    {
        _dbContext = dbContext;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    public async Task<Result<int>> ReencryptPartnerContractsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var contracts = await _dbContext.PartnerContracts
                .ToListAsync(cancellationToken);

            var updatedCount = 0;

            foreach (var contract in contracts)
            {
                var needsUpdate = false;

                // 1. Blind Index ausente ou não preenchido
                if (string.IsNullOrWhiteSpace(contract.LegalDocumentNumberHash) && !string.IsNullOrWhiteSpace(contract.LegalDocumentNumber))
                {
                    var cleanDoc = contract.LegalDocumentNumber.Trim();
                    var hash = _encryptionService.ComputeBlindIndex(cleanDoc);
                    contract.SetLegalDocumentNumberHash(hash);
                    needsUpdate = true;
                }

                if (needsUpdate)
                {
                    _dbContext.PartnerContracts.Update(contract);
                    updatedCount++;
                }
            }

            if (updatedCount > 0)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Re-encriptação e blind indexação de {Count} contratos de parceiros comerciais concluída com sucesso.", updatedCount);
            }

            return Result.Success(updatedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha durante o processo de re-encriptação de contratos de parceiros.");
            return Result.Failure<int>(Error.Failure("Referral.ReencryptionFailed", ex.Message));
        }
    }
}
