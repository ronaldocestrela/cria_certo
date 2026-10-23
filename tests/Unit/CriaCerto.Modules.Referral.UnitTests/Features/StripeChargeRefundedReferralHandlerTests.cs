using CriaCerto.Modules.Referral.Application.Abstractions;
using CriaCerto.Modules.Referral.Application.Domain;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Features.EventHandlers;
using CriaCerto.Modules.Referral.Infrastructure.Persistence;
using CriaCerto.Modules.Tenancy.Application.Events;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CriaCerto.Modules.Referral.UnitTests.Features;

public sealed class StripeChargeRefundedReferralHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ReferralDbContext> _options;

    public StripeChargeRefundedReferralHandlerTests()
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

    private sealed class FakeReferralAuditSink : IReferralAuditSink
    {
        public List<(string Action, string Resource, Guid? TargetTenantId, string DetailsJson)> Entries { get; } = new();

        public Task RecordSystemAuditAsync(
            string action,
            string resource,
            Guid? targetTenantId,
            string detailsJson,
            CancellationToken cancellationToken = default)
        {
            Entries.Add((action, resource, targetTenantId, detailsJson));
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Handle_WhenMatchingPendingCommissionExists_ShouldCancelAndAudit()
    {
        // Arrange
        using var context = new ReferralDbContext(_options);
        await context.Database.EnsureCreatedAsync();

        var partner = ReferralProgramParticipant.CreateCommercialPartner(Guid.NewGuid()).Value;
        var code = ReferralCode.Create(partner.Id, "CODE-REFUND").Value;
        var attribution = ReferralAttribution.Create(code.Id, partner.Id, Guid.NewGuid()).Value;

        context.Participants.Add(partner);
        context.ReferralCodes.Add(code);
        context.Attributions.Add(attribution);

        var ledger = ReferralRewardLedger.CreateCashCommission(
            attribution.Id, partner.Id, 180m, 180m, "BRL", DateTime.UtcNow.AddDays(20), "in_refund_target").Value;

        context.RewardLedger.Add(ledger);
        await context.SaveChangesAsync();

        var auditSink = new FakeReferralAuditSink();
        var handler = new StripeChargeRefundedReferralHandler(context, auditSink);

        var notification = new StripeChargeRefundedIntegrationEvent(
            StripeChargeId: "ch_12345",
            StripeInvoiceId: "in_refund_target",
            StripeCustomerId: "cus_abc",
            AmountRefunded: 180m,
            Currency: "BRL",
            OccurredOnUtc: DateTime.UtcNow);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        var dbLedger = await context.RewardLedger.FindAsync(ledger.Id);
        dbLedger!.Status.Should().Be(LedgerRewardStatus.Cancelled);
        dbLedger.CancellationReason.Should().Contain("ch_12345");

        auditSink.Entries.Should().HaveCount(1);
        auditSink.Entries[0].Action.Should().Be("Referral.CommissionCancelled.WebhookRefund");
    }

    [Fact]
    public async Task Handle_WhenNoMatchingCommission_ShouldDoNothing()
    {
        // Arrange
        using var context = new ReferralDbContext(_options);
        await context.Database.EnsureCreatedAsync();

        var auditSink = new FakeReferralAuditSink();
        var handler = new StripeChargeRefundedReferralHandler(context, auditSink);

        var notification = new StripeChargeRefundedIntegrationEvent(
            StripeChargeId: "ch_unknown",
            StripeInvoiceId: "in_other_invoice",
            StripeCustomerId: "cus_xyz",
            AmountRefunded: 50m,
            Currency: "BRL",
            OccurredOnUtc: DateTime.UtcNow);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        auditSink.Entries.Should().BeEmpty();
    }
}
