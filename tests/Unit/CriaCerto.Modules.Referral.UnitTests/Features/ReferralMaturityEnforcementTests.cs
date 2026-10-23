using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Abstractions;
using CriaCerto.Modules.Referral.Application.Domain;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Features.Enforcement.Services;
using CriaCerto.Modules.Referral.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CriaCerto.Modules.Referral.UnitTests.Features;

public sealed class ReferralMaturityEnforcementTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ReferralDbContext> _options;

    public ReferralMaturityEnforcementTests()
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

    private sealed class FakeTenantInfoLookupService : ITenantInfoLookupService
    {
        private readonly Dictionary<Guid, TenantSummaryDto> _tenants = new();

        public void AddTenant(Guid tenantId, string name, string status)
        {
            _tenants[tenantId] = new TenantSummaryDto(tenantId, name, name, status, "PRO", "Plano Pro");
        }

        public Task<TenantSummaryDto?> GetTenantSummaryAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            _tenants.TryGetValue(tenantId, out var tenant);
            return Task.FromResult(tenant);
        }

        public Task<IReadOnlyDictionary<Guid, TenantSummaryDto>> GetTenantSummariesAsync(
            IEnumerable<Guid> tenantIds,
            CancellationToken cancellationToken = default)
        {
            var result = tenantIds
                .Where(_tenants.ContainsKey)
                .ToDictionary(id => id, id => _tenants[id]);

            return Task.FromResult<IReadOnlyDictionary<Guid, TenantSummaryDto>>(result);
        }
    }

    private sealed class FakePaymentVerificationService : IReferralPaymentVerificationService
    {
        public HashSet<string> RefundedInvoices { get; } = new();

        public Task<Result<bool>> IsInvoiceRefundedOrDisputedAsync(string stripeInvoiceId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result.Success(RefundedInvoices.Contains(stripeInvoiceId)));
        }

        public Task<Result<bool>> HasCardFingerprintCollisionAsync(
            string payingStripeCustomerId,
            string referrerStripeCustomerId,
            string? directPayingFingerprint = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result.Success(false));
        }
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
    public async Task ExecutePassAsync_WhenMatureAndTenantActive_ShouldReleaseForPayout()
    {
        // Arrange
        using var context = new ReferralDbContext(_options);
        await context.Database.EnsureCreatedAsync();

        var partner = ReferralProgramParticipant.CreateCommercialPartner(Guid.NewGuid()).Value;
        var code = ReferralCode.Create(partner.Id, "AGRO-PROMO").Value;
        var referredTenantId = Guid.NewGuid();
        var attribution = ReferralAttribution.Create(code.Id, partner.Id, referredTenantId).Value;

        context.Participants.Add(partner);
        context.ReferralCodes.Add(code);
        context.Attributions.Add(attribution);

        // Ledger com carência vencida (ontem)
        var matureLedger = ReferralRewardLedger.CreateCashCommission(
            attribution.Id, partner.Id, 250m, 250m, "BRL", DateTime.UtcNow.AddDays(-1), "in_123").Value;

        // Ledger com carência ainda não vencida (em 15 dias)
        var futureLedger = ReferralRewardLedger.CreateCashCommission(
            attribution.Id, partner.Id, 150m, 150m, "BRL", DateTime.UtcNow.AddDays(15), "in_456").Value;

        context.RewardLedger.AddRange(matureLedger, futureLedger);
        await context.SaveChangesAsync();

        var tenantLookup = new FakeTenantInfoLookupService();
        tenantLookup.AddTenant(referredTenantId, "Fazenda Alvorada", "Active");

        var paymentVerification = new FakePaymentVerificationService();
        var auditSink = new FakeReferralAuditSink();

        var service = new ReferralMaturityEnforcementService(
            context, tenantLookup, paymentVerification, auditSink);

        // Act
        var result = await service.ExecutePassAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalEvaluated.Should().Be(1);
        result.Value.ReleasedToAvailable.Should().Be(1);
        result.Value.CancelledDueToRefund.Should().Be(0);
        result.Value.CancelledDueToTenantInsolvency.Should().Be(0);

        var dbMature = await context.RewardLedger.FindAsync(matureLedger.Id);
        dbMature!.Status.Should().Be(LedgerRewardStatus.AvailableForPayout);

        var dbFuture = await context.RewardLedger.FindAsync(futureLedger.Id);
        dbFuture!.Status.Should().Be(LedgerRewardStatus.PendingMaturity);
    }

    [Fact]
    public async Task ExecutePassAsync_WhenTenantPastDue_ShouldCancelCommissionAndMarkAttributionChurned()
    {
        // Arrange
        using var context = new ReferralDbContext(_options);
        await context.Database.EnsureCreatedAsync();

        var partner = ReferralProgramParticipant.CreateCommercialPartner(Guid.NewGuid()).Value;
        var code = ReferralCode.Create(partner.Id, "CONSULTOR-SP").Value;
        var referredTenantId = Guid.NewGuid();
        var attribution = ReferralAttribution.Create(code.Id, partner.Id, referredTenantId).Value;

        context.Participants.Add(partner);
        context.ReferralCodes.Add(code);
        context.Attributions.Add(attribution);

        var ledger = ReferralRewardLedger.CreateCashCommission(
            attribution.Id, partner.Id, 300m, 300m, "BRL", DateTime.UtcNow.AddDays(-2), "in_pastdue").Value;

        context.RewardLedger.Add(ledger);
        await context.SaveChangesAsync();

        var tenantLookup = new FakeTenantInfoLookupService();
        tenantLookup.AddTenant(referredTenantId, "Fazenda Santa Fé", "PastDue");

        var paymentVerification = new FakePaymentVerificationService();
        var auditSink = new FakeReferralAuditSink();

        var service = new ReferralMaturityEnforcementService(
            context, tenantLookup, paymentVerification, auditSink);

        // Act
        var result = await service.ExecutePassAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalEvaluated.Should().Be(1);
        result.Value.ReleasedToAvailable.Should().Be(0);
        result.Value.CancelledDueToTenantInsolvency.Should().Be(1);
        result.Value.AttributionsMarkedChurned.Should().Be(1);

        var dbLedger = await context.RewardLedger.FindAsync(ledger.Id);
        dbLedger!.Status.Should().Be(LedgerRewardStatus.Cancelled);
        dbLedger.CancellationReason.Should().Contain("PastDue");

        var dbAttribution = await context.Attributions.FindAsync(attribution.Id);
        dbAttribution!.Status.Should().Be(AttributionStatus.Churned);

        auditSink.Entries.Should().HaveCount(1);
        auditSink.Entries[0].Action.Should().Be("Referral.CommissionCancelled.TenantInsolvent");
        auditSink.Entries[0].TargetTenantId.Should().Be(referredTenantId);
    }

    [Fact]
    public async Task ExecutePassAsync_WhenStripeRefundDetected_ShouldCancelCommissionAndAudit()
    {
        // Arrange
        using var context = new ReferralDbContext(_options);
        await context.Database.EnsureCreatedAsync();

        var partner = ReferralProgramParticipant.CreateCommercialPartner(Guid.NewGuid()).Value;
        var code = ReferralCode.Create(partner.Id, "CANAL-PARCEIRO").Value;
        var referredTenantId = Guid.NewGuid();
        var attribution = ReferralAttribution.Create(code.Id, partner.Id, referredTenantId).Value;

        context.Participants.Add(partner);
        context.ReferralCodes.Add(code);
        context.Attributions.Add(attribution);

        var ledger = ReferralRewardLedger.CreateCashCommission(
            attribution.Id, partner.Id, 200m, 200m, "BRL", DateTime.UtcNow.AddDays(-3), "in_refunded_999").Value;

        context.RewardLedger.Add(ledger);
        await context.SaveChangesAsync();

        var tenantLookup = new FakeTenantInfoLookupService();
        tenantLookup.AddTenant(referredTenantId, "Fazenda Bela Vista", "Active");

        var paymentVerification = new FakePaymentVerificationService();
        paymentVerification.RefundedInvoices.Add("in_refunded_999");

        var auditSink = new FakeReferralAuditSink();

        var service = new ReferralMaturityEnforcementService(
            context, tenantLookup, paymentVerification, auditSink);

        // Act
        var result = await service.ExecutePassAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalEvaluated.Should().Be(1);
        result.Value.ReleasedToAvailable.Should().Be(0);
        result.Value.CancelledDueToRefund.Should().Be(1);
        result.Value.CancelledDueToTenantInsolvency.Should().Be(0);

        var dbLedger = await context.RewardLedger.FindAsync(ledger.Id);
        dbLedger!.Status.Should().Be(LedgerRewardStatus.Cancelled);
        dbLedger.CancellationReason.Should().Contain("in_refunded_999");

        auditSink.Entries.Should().HaveCount(1);
        auditSink.Entries[0].Action.Should().Be("Referral.CommissionCancelled.StripeRefund");
    }

    [Fact]
    public async Task ExecutePassAsync_WhenNoMatureLedgers_ShouldReturnZeroSummaryWithoutErrors()
    {
        // Arrange
        using var context = new ReferralDbContext(_options);
        await context.Database.EnsureCreatedAsync();

        var tenantLookup = new FakeTenantInfoLookupService();
        var service = new ReferralMaturityEnforcementService(context, tenantLookup);

        // Act
        var result = await service.ExecutePassAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalEvaluated.Should().Be(0);
        result.Value.ReleasedToAvailable.Should().Be(0);
        result.Value.CancelledDueToRefund.Should().Be(0);
        result.Value.CancelledDueToTenantInsolvency.Should().Be(0);
    }
}
