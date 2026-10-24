using System.Security.Cryptography;
using System.Text;
using CriaCerto.Api.BackgroundServices;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Abstractions;
using CriaCerto.Modules.Referral.Application.Domain;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Features.Enforcement.Services;
using CriaCerto.Modules.Referral.Application.Features.EventHandlers;
using CriaCerto.Modules.Referral.Application.Options;
using CriaCerto.Modules.Referral.Infrastructure.Persistence;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Events;
using CriaCerto.Modules.Tenancy.Application.Features.ProcessStripeWebhook;
using CriaCerto.Modules.Tenancy.Infrastructure.Persistence;
using CriaCerto.Modules.Tenancy.Infrastructure.Services;
using FluentAssertions;
using MediatR;
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
/// Roadmap 6.2 Testes de Integração: Simulação de ciclo de vida completo via webhooks do Stripe Mock/Sandbox.
/// </summary>
public sealed class ReferralStripeWebhookLifecycleIntegrationTests : IDisposable
{
    private const string WebhookSecret = "whsec_test_secret_referral_stripe_mock_2026";
    private readonly SqliteConnection _tenancyConnection;
    private readonly SqliteConnection _referralConnection;
    private readonly ServiceProvider _serviceProvider;
    private readonly TenancyDbContext _tenancyDb;
    private readonly ReferralDbContext _referralDb;
    private readonly ProcessStripeWebhookCommandHandler _stripeWebhookHandler;
    private readonly List<(string Action, string Resource, Guid? TargetTenantId, string DetailsJson)> _auditLogRecords = new();

    public ReferralStripeWebhookLifecycleIntegrationTests()
    {
        _tenancyConnection = new SqliteConnection("Filename=:memory:");
        _tenancyConnection.Open();

        _referralConnection = new SqliteConnection("Filename=:memory:");
        _referralConnection.Open();

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        // Tenancy DB
        services.AddDbContext<TenancyDbContext>(options => options.UseSqlite(_tenancyConnection));
        services.AddScoped<ITenancyDbContext>(sp => sp.GetRequiredService<TenancyDbContext>());

        // Referral DB
        services.AddDbContext<ReferralDbContext>(options => options.UseSqlite(_referralConnection));
        services.AddScoped<IReferralDbContext>(sp => sp.GetRequiredService<ReferralDbContext>());

        // Options
        var stripeOptions = new StripeOptions
        {
            ApiKey = "sk_test_mock_referral_suite_2026",
            WebhookSecret = WebhookSecret
        };
        services.AddSingleton(Options.Create(stripeOptions));

        var enforcementOptions = Substitute.For<IOptionsMonitor<ReferralEnforcementOptions>>();
        enforcementOptions.CurrentValue.Returns(new ReferralEnforcementOptions
        {
            IntervalHours = 24,
            InitialDelaySeconds = 0
        });
        services.AddSingleton(enforcementOptions);

        // Mocks for cross-module interfaces
        var auditSinkMock = Substitute.For<IReferralAuditSink>();
        auditSinkMock.RecordSystemAuditAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                _auditLogRecords.Add((
                    callInfo.ArgAt<string>(0),
                    callInfo.ArgAt<string>(1),
                    callInfo.ArgAt<Guid?>(2),
                    callInfo.ArgAt<string>(3)
                ));
                return Task.CompletedTask;
            });
        services.AddSingleton(auditSinkMock);

        var paymentVerificationMock = Substitute.For<IReferralPaymentVerificationService>();
        paymentVerificationMock.HasCardFingerprintCollisionAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(false)); // Sem colisão de cartão
        paymentVerificationMock.IsInvoiceRefundedOrDisputedAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(false));
        services.AddSingleton(paymentVerificationMock);

        var tenantLookupMock = Substitute.For<ITenantInfoLookupService>();
        tenantLookupMock.GetTenantSummariesAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var ids = callInfo.ArgAt<IEnumerable<Guid>>(0);
                var dict = new Dictionary<Guid, TenantSummaryDto>();
                using var scope = services.BuildServiceProvider().CreateScope();
                var tDb = scope.ServiceProvider.GetRequiredService<TenancyDbContext>();
                foreach (var id in ids)
                {
                    var t = tDb.Tenants.Find(id);
                    if (t != null)
                    {
                        dict[id] = new TenantSummaryDto(t.Id, t.Name, t.Name, t.Status, t.SubscribedPlan, t.SubscribedPlan, t.StripeCustomerId);
                    }
                }
                return dict;
            });
        services.AddSingleton(tenantLookupMock);

        // MediatR & Handlers
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(TenantPaymentSucceededReferralHandler).Assembly);
            cfg.RegisterServicesFromAssembly(typeof(ProcessStripeWebhookCommandHandler).Assembly);
        });

        // Services & Workers
        services.AddScoped<IReferralMaturityEnforcementService, ReferralMaturityEnforcementService>();
        services.AddScoped<IStripePaymentService, StripePaymentService>();
        services.AddScoped<StripePaymentService>();
        services.AddScoped<ProcessStripeWebhookCommandHandler>();
        services.AddSingleton<ReferralMaturityAndEnforcementWorker>();

        _serviceProvider = services.BuildServiceProvider();

        _tenancyDb = _serviceProvider.GetRequiredService<TenancyDbContext>();
        _tenancyDb.Database.EnsureCreated();

        _referralDb = _serviceProvider.GetRequiredService<ReferralDbContext>();
        _referralDb.Database.EnsureCreated();

        _stripeWebhookHandler = _serviceProvider.GetRequiredService<ProcessStripeWebhookCommandHandler>();
    }

    public void Dispose()
    {
        _tenancyDb.Dispose();
        _referralDb.Dispose();
        _tenancyConnection.Close();
        _tenancyConnection.Dispose();
        _referralConnection.Close();
        _referralConnection.Dispose();
        _serviceProvider.Dispose();
    }

    private static string GenerateStripeSignature(string payload, string secret, long timestamp)
    {
        var signedPayload = $"{timestamp}.{payload}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload));
        var hashHex = Convert.ToHexString(hashBytes).ToLowerInvariant();
        return $"t={timestamp},v1={hashHex}";
    }

    [Fact]
    public async Task CompleteLifecycle_CheckoutPaid_ThenRefunded_Should_ExecuteEndToEndCorrectly()
    {
        // -------------------------------------------------------------
        // ETAPA 1: Setup do Parceiro, Código e Tenant Indicado (Pending)
        // -------------------------------------------------------------
        var partnerId = Guid.NewGuid();
        var partner = ReferralProgramParticipant.CreateCommercialPartner(partnerId).Value;
        var referralCode = ReferralCode.Create(partner.Id, "CONSULTOR-AGRO").Value;

        var partnerContract = PartnerContract.Create(
            partner.Id,
            CommissionType.PercentageFirstMonth,
            commissionValue: 20m,
            maxRecurringMonths: 1,
            paymentTermsDays: 30,
            pixKey: "12345678909",
            pixKeyType: PixKeyType.Cpf,
            legalDocumentNumber: "123.456.789-09",
            validFromUtc: DateTime.UtcNow).Value;

        var referredTenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Indicada pelo Consultor",
            CNPJ = "12.345.678/0001-99",
            Status = "Trial",
            SubscribedPlan = "Pro",
            Capacity = 1000,
            StripeCustomerId = "cus_mock_referred_001",
            StatusReason = "Trial pendente de pagamento",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        var attribution = ReferralAttribution.Create(referralCode.Id, partner.Id, referredTenant.Id).Value;

        _tenancyDb.Tenants.Add(referredTenant);
        await _tenancyDb.SaveChangesAsync();

        _referralDb.Participants.Add(partner);
        _referralDb.ReferralCodes.Add(referralCode);
        _referralDb.PartnerContracts.Add(partnerContract);
        _referralDb.Attributions.Add(attribution);
        await _referralDb.SaveChangesAsync();

        // -------------------------------------------------------------
        // ETAPA 2: Webhook checkout.session.completed (Pagamento Quitado)
        // -------------------------------------------------------------
        var invoiceId = "in_checkout_lifecycle_001";
        var checkoutPayload = $$"""
        {
          "id": "evt_checkout_lifecycle_001",
          "object": "event",
          "type": "checkout.session.completed",
          "data": {
            "object": {
              "id": "cs_test_lifecycle_001",
              "object": "checkout.session",
              "customer": "{{referredTenant.StripeCustomerId}}",
              "subscription": "sub_lifecycle_001",
              "payment_status": "paid",
              "amount_total": 50000,
              "currency": "brl",
              "invoice": "{{invoiceId}}",
              "metadata": {
                "tenant_id": "{{referredTenant.Id}}",
                "plan_code": "Pro",
                "capacity": "1000"
              }
            }
          }
        }
        """;

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signature = GenerateStripeSignature(checkoutPayload, WebhookSecret, timestamp);

        // Act 1: Processar webhook de checkout
        var checkoutResult = await _stripeWebhookHandler.Handle(
            new ProcessStripeWebhookCommand(checkoutPayload, signature), CancellationToken.None);

        // Assert 1: Tenancy ativado e Atribuição convertida com comissão gerada em PendingMaturity (D+30)
        checkoutResult.IsSuccess.Should().BeTrue();

        var refreshedTenant = await _tenancyDb.Tenants.AsNoTracking().FirstAsync(t => t.Id == referredTenant.Id);
        refreshedTenant.Status.Should().Be("Active");

        var refreshedAttribution = await _referralDb.Attributions.AsNoTracking().FirstAsync(a => a.Id == attribution.Id);
        refreshedAttribution.Status.Should().Be(AttributionStatus.Converted);
        refreshedAttribution.ConvertedAtUtc.Should().NotBeNull();

        var rewardLedger = await _referralDb.RewardLedger.AsNoTracking()
            .FirstOrDefaultAsync(r => r.ReferralAttributionId == attribution.Id);
        rewardLedger.Should().NotBeNull();
        rewardLedger!.Status.Should().Be(LedgerRewardStatus.PendingMaturity);
        rewardLedger.GrossAmount.Should().Be(100m); // 20% de R$ 500 = R$ 100
        rewardLedger.MaturityDateUtc.Should().BeCloseTo(DateTime.UtcNow.AddDays(30), TimeSpan.FromMinutes(2));

        // -------------------------------------------------------------
        // ETAPA 3: Webhook charge.refunded (Estorno no Stripe)
        // -------------------------------------------------------------
        var refundPayload = $$"""
        {
          "id": "evt_charge_refund_001",
          "object": "event",
          "type": "charge.refunded",
          "data": {
            "object": {
              "id": "ch_mock_refund_001",
              "object": "charge",
              "customer": "{{referredTenant.StripeCustomerId}}",
              "invoice": "{{invoiceId}}",
              "amount_refunded": 50000,
              "currency": "brl"
            }
          }
        }
        """;

        var refundTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var refundSignature = GenerateStripeSignature(refundPayload, WebhookSecret, refundTimestamp);

        // Act 2: Processar webhook de estorno
        var refundResult = await _stripeWebhookHandler.Handle(
            new ProcessStripeWebhookCommand(refundPayload, refundSignature), CancellationToken.None);

        // Assert 2: Comissão cancelada compulsoriamente e auditoria forense gravada
        refundResult.IsSuccess.Should().BeTrue();

        var reloadedLedger = await _referralDb.RewardLedger.AsNoTracking().FirstAsync(r => r.Id == rewardLedger.Id);
        reloadedLedger.Status.Should().Be(LedgerRewardStatus.Cancelled);
        reloadedLedger.CancellationReason.Should().Contain("ch_mock_refund_001");

        _auditLogRecords.Should().Contain(a =>
            a.Action == "Referral.CommissionCancelled.WebhookRefund" &&
            a.Resource.Contains(reloadedLedger.Id.ToString()));
    }
}
