using CriaCerto.Api.BackgroundServices;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Abstractions;
using CriaCerto.Modules.Referral.Application.Domain;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Features.Enforcement.Services;
using CriaCerto.Modules.Referral.Application.Options;
using CriaCerto.Modules.Referral.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace CriaCerto.Architecture.IntegrationTests;

public class ReferralMaturityWorkerIntegrationTests : IDisposable
{
    private readonly SqliteConnection _referralConnection;
    private readonly ServiceProvider _serviceProvider;
    private readonly ReferralDbContext _dbContext;
    private readonly ITenantInfoLookupService _tenantLookupMock;
    private readonly IReferralPaymentVerificationService _paymentVerificationMock;
    private readonly IReferralAuditSink _auditSinkMock;

    public ReferralMaturityWorkerIntegrationTests()
    {
        _referralConnection = new SqliteConnection("Filename=:memory:");
        _referralConnection.Open();

        _tenantLookupMock = Substitute.For<ITenantInfoLookupService>();
        _paymentVerificationMock = Substitute.For<IReferralPaymentVerificationService>();
        _auditSinkMock = Substitute.For<IReferralAuditSink>();

        var services = new ServiceCollection();

        services.AddDbContext<ReferralDbContext>(options =>
            options.UseSqlite(_referralConnection));

        services.AddScoped<IReferralDbContext>(sp => sp.GetRequiredService<ReferralDbContext>());
        services.AddSingleton(_tenantLookupMock);
        services.AddSingleton(_paymentVerificationMock);
        services.AddSingleton(_auditSinkMock);
        services.AddScoped<IReferralMaturityEnforcementService, ReferralMaturityEnforcementService>();

        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        var optionsMonitorMock = Substitute.For<IOptionsMonitor<ReferralEnforcementOptions>>();
        optionsMonitorMock.CurrentValue.Returns(new ReferralEnforcementOptions
        {
            IntervalHours = 24,
            InitialDelaySeconds = 0
        });
        services.AddSingleton(optionsMonitorMock);

        services.AddSingleton<ReferralMaturityAndEnforcementWorker>();

        _serviceProvider = services.BuildServiceProvider();

        _dbContext = _serviceProvider.GetRequiredService<ReferralDbContext>();
        _dbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _referralConnection.Close();
        _referralConnection.Dispose();
        _serviceProvider.Dispose();
    }

    [Fact]
    public async Task Worker_ProcessEnforcementPassSafelyAsync_ShouldExecuteFullCycleSuccessfully()
    {
        // Arrange
        var partner = ReferralProgramParticipant.CreateCommercialPartner(Guid.NewGuid()).Value;
        var code = ReferralCode.Create(partner.Id, "AGRO-INTEGRATION").Value;
        var activeTenantId = Guid.NewGuid();
        var insolventTenantId = Guid.NewGuid();

        var attributionActive = ReferralAttribution.Create(code.Id, partner.Id, activeTenantId).Value;
        var attributionInsolvent = ReferralAttribution.Create(code.Id, partner.Id, insolventTenantId).Value;

        _dbContext.Participants.Add(partner);
        _dbContext.ReferralCodes.Add(code);
        _dbContext.Attributions.AddRange(attributionActive, attributionInsolvent);

        // Lançamento 1: Elegível e fazenda ativa -> Deve liberar para AvailableForPayout
        var matureLedger = ReferralRewardLedger.CreateCashCommission(
            attributionActive.Id, partner.Id, 200m, 200m, "BRL", DateTime.UtcNow.AddDays(-2), "in_valid").Value;

        // Lançamento 2: Elegível mas fazenda em PastDue -> Deve cancelar compulsoriamente
        var insolventLedger = ReferralRewardLedger.CreateCashCommission(
            attributionInsolvent.Id, partner.Id, 350m, 350m, "BRL", DateTime.UtcNow.AddDays(-1), "in_pastdue").Value;

        // Lançamento 3: Não elegível ainda (vence em 20 dias) -> Deve permanecer PendingMaturity
        var futureLedger = ReferralRewardLedger.CreateCashCommission(
            attributionActive.Id, partner.Id, 100m, 100m, "BRL", DateTime.UtcNow.AddDays(20), "in_future").Value;

        _dbContext.RewardLedger.AddRange(matureLedger, insolventLedger, futureLedger);
        await _dbContext.SaveChangesAsync();

        _tenantLookupMock.GetTenantSummariesAsync(
            Arg.Any<IEnumerable<Guid>>(),
            Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, TenantSummaryDto>
            {
                [activeTenantId] = new TenantSummaryDto(activeTenantId, "Fazenda Ativa", "Fazenda Ativa", "Active", "PRO", "Plano Pro"),
                [insolventTenantId] = new TenantSummaryDto(insolventTenantId, "Fazenda Inadimplente", "Fazenda Inadimplente", "PastDue", "PRO", "Plano Pro")
            });

        _paymentVerificationMock.IsInvoiceRefundedOrDisputedAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(Result.Success(false));

        var worker = _serviceProvider.GetRequiredService<ReferralMaturityAndEnforcementWorker>();

        // Act
        var summary = await worker.ProcessEnforcementPassSafelyAsync(DateTime.UtcNow);

        // Assert
        summary.Should().NotBeNull();
        summary!.TotalEvaluated.Should().Be(2);
        summary.ReleasedToAvailable.Should().Be(1);
        summary.CancelledDueToTenantInsolvency.Should().Be(1);
        summary.CancelledDueToRefund.Should().Be(0);
        summary.AttributionsMarkedChurned.Should().Be(1);

        var refreshedMature = await _dbContext.RewardLedger.AsNoTracking().FirstOrDefaultAsync(l => l.Id == matureLedger.Id);
        refreshedMature!.Status.Should().Be(LedgerRewardStatus.AvailableForPayout);

        var refreshedInsolvent = await _dbContext.RewardLedger.AsNoTracking().FirstOrDefaultAsync(l => l.Id == insolventLedger.Id);
        refreshedInsolvent!.Status.Should().Be(LedgerRewardStatus.Cancelled);
        refreshedInsolvent.CancellationReason.Should().Contain("PastDue");

        var refreshedFuture = await _dbContext.RewardLedger.AsNoTracking().FirstOrDefaultAsync(l => l.Id == futureLedger.Id);
        refreshedFuture!.Status.Should().Be(LedgerRewardStatus.PendingMaturity);

        var refreshedAttributionInsolvent = await _dbContext.Attributions.AsNoTracking().FirstOrDefaultAsync(a => a.Id == attributionInsolvent.Id);
        refreshedAttributionInsolvent!.Status.Should().Be(AttributionStatus.Churned);

        await _auditSinkMock.Received(1).RecordSystemAuditAsync(
            action: "Referral.CommissionCancelled.TenantInsolvent",
            resource: Arg.Is<string>(r => r.Contains(insolventLedger.Id.ToString())),
            targetTenantId: insolventTenantId,
            detailsJson: Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }
}
