using System.Text;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Abstractions;
using CriaCerto.Modules.Referral.Application.Domain;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Domain.Errors;
using CriaCerto.Modules.Referral.Application.Features.Payouts.Commands;
using CriaCerto.Modules.Referral.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CriaCerto.Modules.Referral.UnitTests.Features;

public sealed class PayoutCommandsTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ReferralDbContext> _options;
    private readonly FakePayoutReceiptStorageService _storageService = new();

    public PayoutCommandsTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<ReferralDbContext>()
            .UseSqlite(_connection)
            .Options;
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    [Fact]
    public async Task RegisterPartnerPayoutBatch_WithValidEntries_ShouldMarkAllPaidOut()
    {
        // Arrange
        using var context = new ReferralDbContext(_options);
        await context.Database.EnsureCreatedAsync();

        var partner = ReferralProgramParticipant.CreateCommercialPartner(Guid.NewGuid()).Value;
        var code = ReferralCode.Create(partner.Id, "PARTNER-CODE").Value;
        var attribution = ReferralAttribution.Create(code.Id, partner.Id, Guid.NewGuid()).Value;

        context.Participants.Add(partner);
        context.ReferralCodes.Add(code);
        context.Attributions.Add(attribution);

        var ledger1 = ReferralRewardLedger.CreateCashCommission(attribution.Id, partner.Id, 100m, 100m, "BRL", DateTime.UtcNow.AddDays(-1)).Value;
        var ledger2 = ReferralRewardLedger.CreateCashCommission(attribution.Id, partner.Id, 200m, 200m, "BRL", DateTime.UtcNow.AddDays(-1)).Value;

        ledger1.ReleaseForPayout();
        ledger2.ReleaseForPayout();

        context.RewardLedger.AddRange(ledger1, ledger2);
        await context.SaveChangesAsync();

        var handler = new RegisterPartnerPayoutBatchCommandHandler(context, _storageService);
        var adminId = Guid.NewGuid();

        using var dummyStream = new MemoryStream(Encoding.UTF8.GetBytes("DUMMY RECEIPT"));
        var command = new RegisterPartnerPayoutBatchCommand(
            partner.Id,
            new[] { ledger1.Id, ledger2.Id },
            "PIX_E2E_12345",
            dummyStream,
            "pix_comp.pdf",
            "application/pdf",
            adminId
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(2);

        var updated1 = await context.RewardLedger.FindAsync(ledger1.Id);
        var updated2 = await context.RewardLedger.FindAsync(ledger2.Id);

        updated1!.Status.Should().Be(LedgerRewardStatus.PaidOut);
        updated1.PayoutTransactionReference.Should().Be("PIX_E2E_12345");
        updated1.PayoutReceiptFilePath.Should().Be("receipts/pix_comp.pdf");
        updated1.PayoutRegisteredByAdminId.Should().Be(adminId);

        updated2!.Status.Should().Be(LedgerRewardStatus.PaidOut);
    }

    [Fact]
    public async Task RegisterPartnerPayoutBatch_WhenCommissionBelongsToOtherPartner_ShouldFailAndCleanupReceipt()
    {
        // Arrange
        using var context = new ReferralDbContext(_options);
        await context.Database.EnsureCreatedAsync();

        var partner1 = ReferralProgramParticipant.CreateCommercialPartner(Guid.NewGuid()).Value;
        var partner2 = ReferralProgramParticipant.CreateCommercialPartner(Guid.NewGuid()).Value;
        var code = ReferralCode.Create(partner1.Id, "CODE-P1").Value;
        var attribution = ReferralAttribution.Create(code.Id, partner1.Id, Guid.NewGuid()).Value;

        context.Participants.AddRange(partner1, partner2);
        context.ReferralCodes.Add(code);
        context.Attributions.Add(attribution);

        var ledger1 = ReferralRewardLedger.CreateCashCommission(attribution.Id, partner1.Id, 100m, 100m, "BRL", DateTime.UtcNow.AddDays(-1)).Value;
        var ledger2 = ReferralRewardLedger.CreateCashCommission(attribution.Id, partner2.Id, 200m, 200m, "BRL", DateTime.UtcNow.AddDays(-1)).Value;

        ledger1.ReleaseForPayout();
        ledger2.ReleaseForPayout();

        context.RewardLedger.AddRange(ledger1, ledger2);
        await context.SaveChangesAsync();

        var handler = new RegisterPartnerPayoutBatchCommandHandler(context, _storageService);

        using var dummyStream = new MemoryStream(Encoding.UTF8.GetBytes("DUMMY RECEIPT"));
        var command = new RegisterPartnerPayoutBatchCommand(
            partner1.Id,
            new[] { ledger1.Id, ledger2.Id }, // ledger2 belongs to partner2
            "PIX_123",
            dummyStream,
            "receipt.pdf",
            "application/pdf",
            Guid.NewGuid()
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ReferralErrors.PayoutBatch.InvalidCommissionEntry.Code);
        _storageService.DeletedPaths.Should().Contain("receipts/pix_comp.pdf");
    }

    [Fact]
    public async Task CancelCommissionLedgerEntry_WhenPending_ShouldSetStatusToCancelled()
    {
        // Arrange
        using var context = new ReferralDbContext(_options);
        await context.Database.EnsureCreatedAsync();

        var partner = ReferralProgramParticipant.CreateCommercialPartner(Guid.NewGuid()).Value;
        var code = ReferralCode.Create(partner.Id, "P-CODE").Value;
        var attribution = ReferralAttribution.Create(code.Id, partner.Id, Guid.NewGuid()).Value;

        context.Participants.Add(partner);
        context.ReferralCodes.Add(code);
        context.Attributions.Add(attribution);

        var ledger = ReferralRewardLedger.CreateCashCommission(attribution.Id, partner.Id, 150m, 150m, "BRL", DateTime.UtcNow.AddDays(30)).Value;
        context.RewardLedger.Add(ledger);
        await context.SaveChangesAsync();

        var handler = new CancelCommissionLedgerEntryCommandHandler(context);
        var command = new CancelCommissionLedgerEntryCommand(ledger.Id, "Suspeita de fraude confirmada pelo suporte", Guid.NewGuid());

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var updated = await context.RewardLedger.FindAsync(ledger.Id);
        updated!.Status.Should().Be(LedgerRewardStatus.Cancelled);
        updated.CancellationReason.Should().Be("Suspeita de fraude confirmada pelo suporte");
    }

    private sealed class FakePayoutReceiptStorageService : IPayoutReceiptStorageService
    {
        public bool ShouldFail { get; set; }
        public string SavedPath { get; set; } = "receipts/pix_comp.pdf";
        public List<string> DeletedPaths { get; } = new();

        public Task<Result<string>> SaveReceiptAsync(Stream contentStream, string originalFileName, string contentType, CancellationToken cancellationToken = default)
        {
            if (ShouldFail)
                return Task.FromResult(Result.Failure<string>(ReferralErrors.Receipt.InvalidExtension));

            return Task.FromResult(Result.Success(SavedPath));
        }

        public Task<Result<(Stream Stream, string ContentType, string FileName)>> OpenReceiptStreamAsync(string filePath, CancellationToken cancellationToken = default)
        {
            var ms = new MemoryStream(Encoding.UTF8.GetBytes("DUMMY"));
            return Task.FromResult(Result.Success(((Stream)ms, "application/pdf", "receipt.pdf")));
        }

        public bool DeleteReceipt(string filePath)
        {
            DeletedPaths.Add(filePath);
            return true;
        }
    }
}
