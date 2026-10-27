using CriaCerto.BuildingBlocks.Abstractions.Security;
using CriaCerto.BuildingBlocks.Infrastructure.Security;
using CriaCerto.Modules.Referral.Application.Domain;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Infrastructure.Persistence;
using CriaCerto.Modules.Referral.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CriaCerto.Modules.Referral.UnitTests.Features;

public sealed class PartnerContractEncryptionTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ReferralDbContext> _options;
    private readonly IDataEncryptionService _encryptionService;
    private readonly ReferralDbContext _dbContext;

    public PartnerContractEncryptionTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<ReferralDbContext>()
            .UseSqlite(_connection)
            .Options;

        var encryptionOptions = new EncryptionOptions
        {
            CurrentKey = EncryptionDefaults.DefaultDevKey,
            CurrentKeyVersion = 1,
            BlindIndexKey = EncryptionDefaults.DefaultDevBlindIndexKey
        };
        _encryptionService = new AesGcmDataEncryptionService(encryptionOptions);

        _dbContext = new ReferralDbContext(_options, _encryptionService);
        _dbContext.Database.EnsureCreated();
    }

    [Fact]
    public async Task SaveAndLoad_ShouldEncryptInDatabaseAndDecryptTransparentlyInEntity()
    {
        // Arrange
        var participant = ReferralProgramParticipant.CreateCommercialPartner(Guid.NewGuid()).Value;
        _dbContext.Participants.Add(participant);
        await _dbContext.SaveChangesAsync();

        const string plainPixKey = "financeiro@agronegocios.com.br";
        const string plainDocNumber = "12.345.678/0001-99";
        var docHash = _encryptionService.ComputeBlindIndex(plainDocNumber);

        var contractResult = PartnerContract.Create(
            participantId: participant.Id,
            commissionType: CommissionType.PercentageRecurring,
            commissionValue: 15.0m,
            maxRecurringMonths: 12,
            paymentTermsDays: 30,
            pixKey: plainPixKey,
            pixKeyType: PixKeyType.Email,
            legalDocumentNumber: plainDocNumber,
            validFromUtc: DateTime.UtcNow,
            legalDocumentNumberHash: docHash);

        contractResult.IsSuccess.Should().BeTrue();
        var contract = contractResult.Value;

        // Act
        _dbContext.PartnerContracts.Add(contract);
        await _dbContext.SaveChangesAsync();

        // 1. Verificação direta no banco (Raw SQL / ADO.NET) para provar criptografia em repouso
        using (var rawCmd = _connection.CreateCommand())
        {
            rawCmd.CommandText = "SELECT PixKey, LegalDocumentNumber, LegalDocumentNumberHash FROM PartnerContracts";

            using var reader = await rawCmd.ExecuteReaderAsync();
            var read = await reader.ReadAsync();
            read.Should().BeTrue();

            var rawPixKey = reader.GetString(0);
            var rawDocNumber = reader.GetString(1);
            var rawDocHash = reader.GetString(2);

            // Validações em repouso (LGPD Art. 46)
            rawPixKey.Should().StartWith(EncryptionDefaults.EnvelopePrefix);
            rawPixKey.Should().NotContain(plainPixKey);

            rawDocNumber.Should().StartWith(EncryptionDefaults.EnvelopePrefix);
            rawDocNumber.Should().NotContain(plainDocNumber);

            rawDocHash.Should().Be(docHash);
        }

        // 2. Verificação de decifragem transparente via novo DbContext
        using var readContext = new ReferralDbContext(_options, _encryptionService);
        var loaded = await readContext.PartnerContracts.FirstOrDefaultAsync(c => c.Id == contract.Id);

        loaded.Should().NotBeNull();
        loaded!.PixKey.Should().Be(plainPixKey);
        loaded.LegalDocumentNumber.Should().Be(plainDocNumber);
        loaded.LegalDocumentNumberHash.Should().Be(docHash);
    }

    [Fact]
    public async Task QueryByLegalDocumentNumberHash_ShouldFindPartnerContractEfficiently()
    {
        // Arrange
        var participant = ReferralProgramParticipant.CreateCommercialPartner(Guid.NewGuid()).Value;
        _dbContext.Participants.Add(participant);
        await _dbContext.SaveChangesAsync();

        const string plainDocNumber = "987.654.321-00";
        var docHash = _encryptionService.ComputeBlindIndex(plainDocNumber);

        var contract = PartnerContract.Create(
            participantId: participant.Id,
            commissionType: CommissionType.FixedPerConversion,
            commissionValue: 150m,
            maxRecurringMonths: null,
            paymentTermsDays: 30,
            pixKey: "98765432100",
            pixKeyType: PixKeyType.Cpf,
            legalDocumentNumber: plainDocNumber,
            validFromUtc: DateTime.UtcNow,
            legalDocumentNumberHash: docHash).Value;

        _dbContext.PartnerContracts.Add(contract);
        await _dbContext.SaveChangesAsync();

        // Act: Consulta exata $O(1)$ por Blind Index
        using var queryContext = new ReferralDbContext(_options, _encryptionService);
        var found = await queryContext.PartnerContracts
            .FirstOrDefaultAsync(c => c.LegalDocumentNumberHash == docHash);

        // Assert
        found.Should().NotBeNull();
        found!.Id.Should().Be(contract.Id);
        found.LegalDocumentNumber.Should().Be(plainDocNumber);
    }

    [Fact]
    public async Task ReencryptPartnerContractsAsync_ShouldIndexLegacyContractsWithMissingHash()
    {
        // Arrange: Inserir contrato sem hash
        var participant = ReferralProgramParticipant.CreateCommercialPartner(Guid.NewGuid()).Value;
        _dbContext.Participants.Add(participant);
        await _dbContext.SaveChangesAsync();

        const string plainDocNumber = "55.666.777/0001-88";
        var contract = PartnerContract.Create(
            participantId: participant.Id,
            commissionType: CommissionType.PercentageFirstMonth,
            commissionValue: 20m,
            maxRecurringMonths: 1,
            paymentTermsDays: 30,
            pixKey: "pix@chave.com",
            pixKeyType: PixKeyType.Email,
            legalDocumentNumber: plainDocNumber,
            validFromUtc: DateTime.UtcNow,
            legalDocumentNumberHash: string.Empty).Value;

        _dbContext.PartnerContracts.Add(contract);
        await _dbContext.SaveChangesAsync();

        contract.LegalDocumentNumberHash.Should().BeEmpty();

        // Act: Executar serviço de re-encriptação
        var reencryptService = new ReferralDataReencryptionService(_dbContext, _encryptionService, NullLogger<ReferralDataReencryptionService>.Instance);
        var result = await reencryptService.ReencryptPartnerContractsAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(1);

        using var verifyContext = new ReferralDbContext(_options, _encryptionService);
        var updated = await verifyContext.PartnerContracts.FindAsync(contract.Id);
        updated.Should().NotBeNull();
        var expectedHash = _encryptionService.ComputeBlindIndex(plainDocNumber);
        updated!.LegalDocumentNumberHash.Should().Be(expectedHash);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }
}
