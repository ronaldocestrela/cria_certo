using CriaCerto.Api.BackgroundServices;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Abstractions;
using CriaCerto.Modules.Referral.Application.Domain;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Features.Enforcement.Services;
using CriaCerto.Modules.Referral.Application.Features.EventHandlers;
using CriaCerto.Modules.Referral.Application.Options;
using CriaCerto.Modules.Referral.Infrastructure.Persistence;
using CriaCerto.Modules.Tenancy.Application.Events;
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

/// <summary>
/// Roadmap 6.2 Testes de Integração: Teste de fluxo de carência D+30 e concorrência de pagamentos.
/// </summary>
public sealed class ReferralMaturityAndPaymentConcurrencyIntegrationTests : IDisposable
{
    private readonly SqliteConnection _referralConnection;
    private readonly ServiceProvider _serviceProvider;
    private readonly ReferralDbContext _dbContext;
    private readonly ITenantInfoLookupService _tenantLookupMock;
    private readonly IReferralPaymentVerificationService _paymentVerificationMock;
    private readonly IReferralAuditSink _auditSinkMock;

    public ReferralMaturityAndPaymentConcurrencyIntegrationTests()
    {
        _referralConnection = new SqliteConnection("Filename=:memory:");
        _referralConnection.Open();

        _tenantLookupMock = Substitute.For<ITenantInfoLookupService>();
        _paymentVerificationMock = Substitute.For<IReferralPaymentVerificationService>();
        _auditSinkMock = Substitute.For<IReferralAuditSink>();

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        services.AddDbContext<ReferralDbContext>(options =>
            options.UseSqlite(_referralConnection));

        services.AddScoped<IReferralDbContext>(sp => sp.GetRequiredService<ReferralDbContext>());
        services.AddSingleton(_tenantLookupMock);
        services.AddSingleton(_paymentVerificationMock);
        services.AddSingleton(_auditSinkMock);
        services.AddScoped<IReferralMaturityEnforcementService, ReferralMaturityEnforcementService>();
        services.AddScoped<TenantPaymentSucceededReferralHandler>();

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
    public async Task DPlus30_MaturityFlow_Should_HoldUntil30Days_Then_ReleaseToAvailableForPayout()
    {
        // Arrange
        var partner = ReferralProgramParticipant.CreateCommercialPartner(Guid.NewGuid()).Value;
        var code = ReferralCode.Create(partner.Id, "MATURITY-TEST").Value;
        var tenantId = Guid.NewGuid();

        var attribution = ReferralAttribution.Create(code.Id, partner.Id, tenantId).Value;
        attribution.MarkConverted(DateTime.UtcNow);

        var baselineTime = DateTime.UtcNow;
        var maturityDate = baselineTime.AddDays(30);

        var commission = ReferralRewardLedger.CreateCashCommission(
            attribution.Id,
            partner.Id,
            grossAmount: 300m,
            netAmount: 300m,
            currency: "BRL",
            maturityDateUtc: maturityDate,
            stripeInvoiceId: "in_maturity_d30").Value;

        _dbContext.Participants.Add(partner);
        _dbContext.ReferralCodes.Add(code);
        _dbContext.Attributions.Add(attribution);
        _dbContext.RewardLedger.Add(commission);
        await _dbContext.SaveChangesAsync();

        _tenantLookupMock.GetTenantSummariesAsync(
            Arg.Any<IEnumerable<Guid>>(),
            Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, TenantSummaryDto>
            {
                [tenantId] = new TenantSummaryDto(tenantId, "Fazenda Ativa", "Fazenda Ativa", "Active", "PRO", "Plano Pro")
            });

        _paymentVerificationMock.IsInvoiceRefundedOrDisputedAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(Result.Success(false));

        var worker = _serviceProvider.GetRequiredService<ReferralMaturityAndEnforcementWorker>();

        // Act 1: Execução no Dia 15 (antes da maturação D+30)
        var passDay15 = await worker.ProcessEnforcementPassSafelyAsync(baselineTime.AddDays(15));

        // Assert 1: Deve ser ignorado pelo filtro de elegibilidade e permanecer em PendingMaturity
        passDay15!.TotalEvaluated.Should().Be(0);
        passDay15.ReleasedToAvailable.Should().Be(0);

        var ledgerDay15 = await _dbContext.RewardLedger.AsNoTracking().FirstAsync(r => r.Id == commission.Id);
        ledgerDay15.Status.Should().Be(LedgerRewardStatus.PendingMaturity);

        // Act 2: Execução no Dia 31 (após a maturação D+30)
        var passDay31 = await worker.ProcessEnforcementPassSafelyAsync(baselineTime.AddDays(31));

        // Assert 2: Transição compulsória para AvailableForPayout
        passDay31!.TotalEvaluated.Should().Be(1);
        passDay31.ReleasedToAvailable.Should().Be(1);

        var ledgerDay31 = await _dbContext.RewardLedger.AsNoTracking().FirstAsync(r => r.Id == commission.Id);
        ledgerDay31.Status.Should().Be(LedgerRewardStatus.AvailableForPayout);
    }

    [Fact]
    public async Task PaymentConcurrency_Should_PreventDuplicateRewards_OnSimultaneousWebhooks()
    {
        // Arrange
        var partner = ReferralProgramParticipant.CreateCommercialPartner(Guid.NewGuid()).Value;
        var code = ReferralCode.Create(partner.Id, "CONCURRENT-PAY").Value;
        var tenantId = Guid.NewGuid();

        var contract = PartnerContract.Create(
            partner.Id,
            CommissionType.FixedPerConversion,
            commissionValue: 150m,
            maxRecurringMonths: null,
            paymentTermsDays: 30,
            pixKey: "12345678909",
            pixKeyType: PixKeyType.Cpf,
            legalDocumentNumber: "123.456.789-09",
            validFromUtc: DateTime.UtcNow).Value;

        var attribution = ReferralAttribution.Create(code.Id, partner.Id, tenantId).Value;

        _dbContext.Participants.Add(partner);
        _dbContext.ReferralCodes.Add(code);
        _dbContext.PartnerContracts.Add(contract);
        _dbContext.Attributions.Add(attribution);
        await _dbContext.SaveChangesAsync();

        var eventNotification = new TenantPaymentSucceededIntegrationEvent(
            TenantId: tenantId,
            StripeCustomerId: "cus_concurrent_01",
            StripeInvoiceId: "in_concurrent_same_invoice_123",
            StripePaymentMethodId: "pm_test",
            StripeCardFingerprint: "card_fp_ok",
            AmountPaid: 500m,
            Currency: "BRL",
            PaymentDateUtc: DateTime.UtcNow
        );

        // Act: Disparar 5 requisições concorrentes para a mesma fatura
        var tasks = Enumerable.Range(1, 5).Select(async _ =>
        {
            using var scope = _serviceProvider.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<TenantPaymentSucceededReferralHandler>();
            await handler.Handle(eventNotification, CancellationToken.None);
        });

        await Task.WhenAll(tasks);

        // Assert: Apenas UMA recompensa foi registrada no ledger para a mesma fatura
        var rewards = await _dbContext.RewardLedger.AsNoTracking()
            .Where(r => r.ReferralAttributionId == attribution.Id)
            .ToListAsync();

        rewards.Should().HaveCount(1);
        rewards[0].GrossAmount.Should().Be(150m);
        rewards[0].StripeInvoiceId.Should().Be("in_concurrent_same_invoice_123");

        var reloadedAttribution = await _dbContext.Attributions.AsNoTracking().FirstAsync(a => a.Id == attribution.Id);
        reloadedAttribution.Status.Should().Be(AttributionStatus.Converted);
    }

    [Fact]
    public async Task CommercialPartner_RecurringCycles_Should_CalculateEachMonth_And_HaltAfterMaxMonths()
    {
        // Arrange: Parceiro com 20% recorrente por até 3 meses
        var partner = ReferralProgramParticipant.CreateCommercialPartner(Guid.NewGuid()).Value;
        var code = ReferralCode.Create(partner.Id, "RECURRING-3MO").Value;
        var tenantId = Guid.NewGuid();

        var contract = PartnerContract.Create(
            partner.Id,
            CommissionType.PercentageRecurring,
            commissionValue: 20m,
            maxRecurringMonths: 3,
            paymentTermsDays: 30,
            pixKey: "12345678909",
            pixKeyType: PixKeyType.Cpf,
            legalDocumentNumber: "123.456.789-09",
            validFromUtc: DateTime.UtcNow).Value;

        var attribution = ReferralAttribution.Create(code.Id, partner.Id, tenantId).Value;

        _dbContext.Participants.Add(partner);
        _dbContext.ReferralCodes.Add(code);
        _dbContext.PartnerContracts.Add(contract);
        _dbContext.Attributions.Add(attribution);
        await _dbContext.SaveChangesAsync();

        // Act & Assert 1: Mês 1 (Ciclo 1)
        using (var scope = _serviceProvider.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<TenantPaymentSucceededReferralHandler>();
            await handler.Handle(new TenantPaymentSucceededIntegrationEvent(
                tenantId, "cus_rec_1", "in_month_1", null, null, 500m, "BRL", DateTime.UtcNow), CancellationToken.None);
        }

        // Act & Assert 2: Mês 2 (Ciclo 2)
        using (var scope = _serviceProvider.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<TenantPaymentSucceededReferralHandler>();
            await handler.Handle(new TenantPaymentSucceededIntegrationEvent(
                tenantId, "cus_rec_1", "in_month_2", null, null, 500m, "BRL", DateTime.UtcNow.AddMonths(1)), CancellationToken.None);
        }

        // Act & Assert 3: Mês 3 (Ciclo 3)
        using (var scope = _serviceProvider.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<TenantPaymentSucceededReferralHandler>();
            await handler.Handle(new TenantPaymentSucceededIntegrationEvent(
                tenantId, "cus_rec_1", "in_month_3", null, null, 500m, "BRL", DateTime.UtcNow.AddMonths(2)), CancellationToken.None);
        }

        // Act & Assert 4: Mês 4 (Ciclo 4 - Deve exceder o teto de 3 meses e NÃO criar recompensa)
        using (var scope = _serviceProvider.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<TenantPaymentSucceededReferralHandler>();
            await handler.Handle(new TenantPaymentSucceededIntegrationEvent(
                tenantId, "cus_rec_1", "in_month_4", null, null, 500m, "BRL", DateTime.UtcNow.AddMonths(3)), CancellationToken.None);
        }

        var allRewards = await _dbContext.RewardLedger.AsNoTracking()
            .Where(r => r.ReferralAttributionId == attribution.Id)
            .OrderBy(r => r.CreatedAtUtc)
            .ToListAsync();

        allRewards.Should().HaveCount(3); // Exatamente 3 meses comissionados!
        allRewards.Select(r => r.GrossAmount).Should().AllBeEquivalentTo(100m); // 20% de R$ 500
        allRewards.Select(r => r.StripeInvoiceId).Should().Contain(new[] { "in_month_1", "in_month_2", "in_month_3" });
        allRewards.Select(r => r.StripeInvoiceId).Should().NotContain("in_month_4");
    }
}
