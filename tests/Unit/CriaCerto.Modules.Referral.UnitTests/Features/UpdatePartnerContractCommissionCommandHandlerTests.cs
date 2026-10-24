using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Abstractions;
using CriaCerto.Modules.Referral.Application.Domain;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Features.CommercialPartners.Commands;
using CriaCerto.Modules.Referral.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace CriaCerto.Modules.Referral.UnitTests.Features;

public sealed class UpdatePartnerContractCommissionCommandHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ReferralDbContext _dbContext;

    public UpdatePartnerContractCommissionCommandHandlerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ReferralDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new ReferralDbContext(options);
        _dbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Handle_ShouldClosePreviousContractAndCreateNewContractWithImmediateEffect()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var participant = ReferralProgramParticipant.CreateCommercialPartner(adminId).Value;
        var profile = CommercialPartnerProfile.Create(
            participant.Id,
            "Dr. Roberto Antunes",
            "Antunes Zootecnia",
            "roberto@antunes.com",
            "+5567999887766",
            PartnerCategory.ZootechnicianConsultant,
            "Campo Grande",
            "MS").Value;

        var contractV1 = PartnerContract.Create(
            participant.Id,
            CommissionType.PercentageRecurring,
            20m,
            12,
            30,
            "roberto@antunes.com",
            PixKeyType.Email,
            "52.998.224/0001-90",
            DateTime.UtcNow.AddMonths(-3)).Value;

        var code = ReferralCode.Create(participant.Id, "ANTUNES-AGRO").Value;

        _dbContext.Participants.Add(participant);
        _dbContext.PartnerProfiles.Add(profile);
        _dbContext.PartnerContracts.Add(contractV1);
        _dbContext.ReferralCodes.Add(code);
        await _dbContext.SaveChangesAsync();

        var handler = new UpdatePartnerContractCommissionCommandHandler(_dbContext);

        var command = new UpdatePartnerContractCommissionCommand(
            ParticipantId: participant.Id,
            CommissionType: CommissionType.PercentageRecurring,
            CommissionValue: 15m, // Renegociado para 15%
            MaxRecurringMonths: 24, // Ampliado para 24 meses
            PaymentTermsDays: 15,
            PixKey: "nova.chave@antunes.com",
            PixKeyType: PixKeyType.Email,
            AdminUserId: adminId,
            Justification: "Renegociação de comissão com extensão de prazo aprovada");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CommissionValue.Should().Be(15m);
        result.Value.MaxRecurringMonths.Should().Be(24);
        result.Value.PaymentTermsDays.Should().Be(15);
        result.Value.PixKey.Should().Be("nova.chave@antunes.com");

        // Verify that contract V1 is now closed
        var reloadedV1 = await _dbContext.PartnerContracts.FindAsync(contractV1.Id);
        reloadedV1.Should().NotBeNull();
        reloadedV1!.IsActive.Should().BeFalse();
        reloadedV1.ValidToUtc.Should().NotBeNull();

        // Verify that contract V2 is active
        var activeContracts = await _dbContext.PartnerContracts
            .Where(c => c.ParticipantId == participant.Id && c.IsActive)
            .ToListAsync();

        activeContracts.Should().HaveCount(1);
        activeContracts[0].CommissionValue.Should().Be(15m);
        activeContracts[0].MaxRecurringMonths.Should().Be(24);
    }

    [Fact]
    public async Task Handle_Should_Call_AuditSink_With_Chained_Forensic_Data_When_Commission_Updated()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var participant = ReferralProgramParticipant.CreateCommercialPartner(adminId).Value;
        var profile = CommercialPartnerProfile.Create(
            participant.Id,
            "Veterinária Campo Limpo",
            "VET Campo",
            "vet@campolimpo.com",
            "+5567999112233",
            PartnerCategory.Veterinarian,
            "Dourados",
            "MS").Value;

        var contractV1 = PartnerContract.Create(
            participant.Id,
            CommissionType.FixedPerConversion,
            100m,
            null,
            30,
            "vet@campolimpo.com",
            PixKeyType.Email,
            "12.345.678/0001-99",
            DateTime.UtcNow.AddMonths(-1)).Value;

        _dbContext.Participants.Add(participant);
        _dbContext.PartnerProfiles.Add(profile);
        _dbContext.PartnerContracts.Add(contractV1);
        await _dbContext.SaveChangesAsync();

        var auditSinkMock = NSubstitute.Substitute.For<IReferralAuditSink>();
        var handler = new UpdatePartnerContractCommissionCommandHandler(_dbContext, auditSinkMock);

        var command = new UpdatePartnerContractCommissionCommand(
            ParticipantId: participant.Id,
            CommissionType: CommissionType.PercentageRecurring,
            CommissionValue: 25m,
            MaxRecurringMonths: 12,
            PaymentTermsDays: 30,
            PixKey: "vet@campolimpo.com",
            PixKeyType: PixKeyType.Email,
            AdminUserId: adminId,
            Justification: "Ajuste de comissão aprovado por 4-Eyes pelo Diretor Comercial");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        await auditSinkMock.Received(1).RecordSystemAuditAsync(
            "Referral.PartnerContract.CommissionUpdated",
            NSubstitute.Arg.Is<string>(s => s.StartsWith("PartnerContract/")),
            null,
            NSubstitute.Arg.Is<string>(s => s.Contains("PartnerContractCommissionUpdated") && s.Contains("25") && s.Contains("4-Eyes")),
            NSubstitute.Arg.Any<CancellationToken>());
    }
}
