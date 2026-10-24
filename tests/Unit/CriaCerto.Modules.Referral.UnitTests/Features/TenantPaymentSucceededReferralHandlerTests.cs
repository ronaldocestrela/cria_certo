using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Abstractions;
using CriaCerto.Modules.Referral.Application.Domain;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Features.EventHandlers;
using CriaCerto.Modules.Referral.Infrastructure.Persistence;
using CriaCerto.Modules.Tenancy.Application.Events;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace CriaCerto.Modules.Referral.UnitTests.Features;

public sealed class TenantPaymentSucceededReferralHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ReferralDbContext _dbContext;

    public TenantPaymentSucceededReferralHandlerTests()
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
    public async Task Handle_Should_Mark_Attribution_Converted_And_Create_Reward_When_Valid_Payment()
    {
        // Arrange
        var referrerTenantId = Guid.NewGuid();
        var referrerParticipant = ReferralProgramParticipant.CreateCustomer(referrerTenantId).Value;
        var referralCode = ReferralCode.Create(referrerParticipant.Id, "BOI-GORDO").Value;
        var payingTenantId = Guid.NewGuid();

        var attribution = ReferralAttribution.Create(referralCode.Id, referrerParticipant.Id, payingTenantId).Value;
        var policy = PlanReferralDiscountPolicy.Create(Guid.NewGuid(), "STARTER", DiscountType.FixedValue, 50m, 100m).Value;

        _dbContext.Participants.Add(referrerParticipant);
        _dbContext.ReferralCodes.Add(referralCode);
        _dbContext.Attributions.Add(attribution);
        _dbContext.PlanDiscountPolicies.Add(policy);
        await _dbContext.SaveChangesAsync();

        var paymentVerificationMock = Substitute.For<IReferralPaymentVerificationService>();
        var tenantLookupMock = Substitute.For<ITenantInfoLookupService>();
        var auditSinkMock = Substitute.For<IReferralAuditSink>();

        tenantLookupMock.GetTenantSummaryAsync(referrerTenantId, Arg.Any<CancellationToken>())
            .Returns(new TenantSummaryDto(
                referrerTenantId, "Fazenda Indicadora", null, "Active", "STARTER", "Starter",
                StripeCustomerId: "cus_referrer_123", PlanBasePrice: 200m));

        paymentVerificationMock.HasCardFingerprintCollisionAsync(
            "cus_paying_999", "cus_referrer_123", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(false)); // Sem colisão

        var handler = new TenantPaymentSucceededReferralHandler(
            _dbContext,
            paymentVerificationMock,
            tenantLookupMock,
            auditSinkMock);

        var notification = new TenantPaymentSucceededIntegrationEvent(
            TenantId: payingTenantId,
            StripeCustomerId: "cus_paying_999",
            StripeInvoiceId: "in_test_123",
            StripePaymentMethodId: "pm_test_123",
            StripeCardFingerprint: "card_fp_paying",
            AmountPaid: 200m,
            Currency: "BRL",
            PaymentDateUtc: DateTime.UtcNow
        );

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        var reloadedAttr = await _dbContext.Attributions.FindAsync(attribution.Id);
        reloadedAttr.Should().NotBeNull();
        reloadedAttr!.Status.Should().Be(AttributionStatus.Converted);
        reloadedAttr.ConvertedAtUtc.Should().NotBeNull();

        var rewards = await _dbContext.RewardLedger
            .Where(r => r.ReferralAttributionId == attribution.Id)
            .ToListAsync();

        rewards.Should().HaveCount(1);
        rewards[0].GrossAmount.Should().Be(50m);
        rewards[0].RewardType.Should().Be(RewardType.InvoiceDiscountCredit);
    }

    [Fact]
    public async Task Handle_Should_Flag_Fraud_And_Suspend_Participant_When_Card_Fingerprint_Collides()
    {
        // Arrange
        var referrerTenantId = Guid.NewGuid();
        var referrerParticipant = ReferralProgramParticipant.CreateCustomer(referrerTenantId).Value;
        var referralCode = ReferralCode.Create(referrerParticipant.Id, "CARD-CLONE").Value;
        var payingTenantId = Guid.NewGuid();

        var attribution = ReferralAttribution.Create(referralCode.Id, referrerParticipant.Id, payingTenantId).Value;

        _dbContext.Participants.Add(referrerParticipant);
        _dbContext.ReferralCodes.Add(referralCode);
        _dbContext.Attributions.Add(attribution);
        await _dbContext.SaveChangesAsync();

        var paymentVerificationMock = Substitute.For<IReferralPaymentVerificationService>();
        var tenantLookupMock = Substitute.For<ITenantInfoLookupService>();
        var auditSinkMock = Substitute.For<IReferralAuditSink>();

        tenantLookupMock.GetTenantSummaryAsync(referrerTenantId, Arg.Any<CancellationToken>())
            .Returns(new TenantSummaryDto(
                referrerTenantId, "Fazenda Indicadora", null, "Active", "STARTER", "Starter",
                StripeCustomerId: "cus_referrer_duplicate_card"));

        paymentVerificationMock.HasCardFingerprintCollisionAsync(
            "cus_paying_same_card", "cus_referrer_duplicate_card", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(true)); // Colisão detectada!

        var handler = new TenantPaymentSucceededReferralHandler(
            _dbContext,
            paymentVerificationMock,
            tenantLookupMock,
            auditSinkMock);

        var notification = new TenantPaymentSucceededIntegrationEvent(
            TenantId: payingTenantId,
            StripeCustomerId: "cus_paying_same_card",
            StripeInvoiceId: "in_fraud_123",
            StripePaymentMethodId: "pm_fraud_123",
            StripeCardFingerprint: "card_fp_collision",
            AmountPaid: 200m,
            Currency: "BRL",
            PaymentDateUtc: DateTime.UtcNow
        );

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        var reloadedAttr = await _dbContext.Attributions.FindAsync(attribution.Id);
        reloadedAttr.Should().NotBeNull();
        reloadedAttr!.Status.Should().Be(AttributionStatus.Fraudulent);
        reloadedAttr.FraudReason.Should().Contain("mesmo cartão de crédito");

        var reloadedParticipant = await _dbContext.Participants.FindAsync(referrerParticipant.Id);
        reloadedParticipant.Should().NotBeNull();
        reloadedParticipant!.Status.Should().Be(ParticipantStatus.Suspended);
        reloadedParticipant.SuspensionReason.Should().Contain("Suspensão preventiva automática");

        // Verify audit log call
        await auditSinkMock.Received(1).RecordSystemAuditAsync(
            "Referral.AntiFraud.CardFingerprintCollision",
            Arg.Is<string>(s => s.Contains("ReferralAttribution")),
            payingTenantId,
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());

        // Verify no reward was created
        var rewards = await _dbContext.RewardLedger
            .Where(r => r.ReferralAttributionId == attribution.Id)
            .ToListAsync();

        rewards.Should().BeEmpty();
    }
}
