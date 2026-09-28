using System.Security.Cryptography;
using System.Text;
using CriaCerto.BuildingBlocks.Abstractions.Licensing;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Features.ProcessStripeWebhook;
using CriaCerto.Modules.Tenancy.Infrastructure.Persistence;
using CriaCerto.Modules.Tenancy.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CriaCerto.Architecture.IntegrationTests;

public class StripeWebhookIntegrationTests : IDisposable
{
    private const string WebhookSecret = "whsec_test_secret_integration_stripe_cli_2026";
    private readonly SqliteConnection _sqliteConnection;
    private readonly TenancyDbContext _dbContext;
    private readonly ProcessStripeWebhookCommandHandler _handler;

    public StripeWebhookIntegrationTests()
    {
        _sqliteConnection = new SqliteConnection("Filename=:memory:");
        _sqliteConnection.Open();

        var dbOptions = new DbContextOptionsBuilder<TenancyDbContext>()
            .UseSqlite(_sqliteConnection)
            .Options;

        _dbContext = new TenancyDbContext(dbOptions);
        _dbContext.Database.EnsureCreated();

        var stripeOptions = new StripeOptions
        {
            ApiKey = "sk_test_integration_mock_cli",
            WebhookSecret = WebhookSecret
        };

        var service = new StripePaymentService(
            _dbContext,
            Options.Create(stripeOptions),
            NullLogger<StripePaymentService>.Instance);

        _handler = new ProcessStripeWebhookCommandHandler(service);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _sqliteConnection.Close();
        _sqliteConnection.Dispose();
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
    public async Task Webhook_CheckoutSessionCompleted_Paid_Should_ActivateTenant_UpgradePlan_And_RecordHistory()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Integração Stripe",
            CNPJ = "12.345.678/0001-90",
            Status = "Trial",
            SubscribedPlan = "Starter",
            Capacity = 500,
            StatusReason = "Período de avaliação",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        var customerId = "cus_int_test_001";
        var subscriptionId = "sub_int_test_001";
        var payload = $$"""
        {
          "id": "evt_int_checkout_completed_001",
          "object": "event",
          "type": "checkout.session.completed",
          "data": {
            "object": {
              "id": "cs_int_001",
              "object": "checkout.session",
              "customer": "{{customerId}}",
              "subscription": "{{subscriptionId}}",
              "payment_status": "paid",
              "metadata": {
                "TenantId": "{{tenant.Id}}",
                "PlanId": "Pro",
                "PlanName": "Pro Fazenda"
              }
            }
          }
        }
        """;

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signature = GenerateStripeSignature(payload, WebhookSecret, timestamp);

        // Act
        var result = await _handler.Handle(new ProcessStripeWebhookCommand(payload, signature), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.EventType.Should().Be("checkout.session.completed");

        var updatedTenant = await _dbContext.Tenants.AsNoTracking().FirstAsync(t => t.Id == tenant.Id);
        updatedTenant.Status.Should().Be("Active");
        updatedTenant.StatusReason.Should().BeNull();
        updatedTenant.SubscribedPlan.Should().Be("Pro");
        updatedTenant.Capacity.Should().Be(2500);
        updatedTenant.StripeCustomerId.Should().Be(customerId);
        updatedTenant.StripeSubscriptionId.Should().Be(subscriptionId);

        // Idempotency event saved
        var eventRecord = await _dbContext.StripeWebhookEvents.FirstOrDefaultAsync(e => e.EventId == "evt_int_checkout_completed_001");
        eventRecord.Should().NotBeNull();
        eventRecord!.EventType.Should().Be("checkout.session.completed");

        // Subscription history recorded
        var history = await _dbContext.SubscriptionHistories
            .FirstOrDefaultAsync(h => h.TenantId == tenant.Id && h.ActionType == SubscriptionActionType.NewSubscription);
        history.Should().NotBeNull();
        history!.SnapshotHeadCount.Should().Be(2500);
        history.Justification.Should().Contain("Assinatura ativada via Stripe Checkout");
    }

    [Fact]
    public async Task Webhook_InvoicePaid_Should_RenewPeriod_And_RecordHistory()
    {
        // Arrange
        var customerId = "cus_int_invoice_paid_002";
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Renovação",
            CNPJ = "12.345.678/0001-91",
            Status = "Active",
            SubscribedPlan = "Pro",
            Capacity = 2500,
            StripeCustomerId = customerId,
            CurrentPeriodEndUtc = DateTime.UtcNow.AddDays(2),
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        var periodEndTimestamp = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds();
        var payload = $$"""
        {
          "id": "evt_int_invoice_paid_002",
          "object": "event",
          "type": "invoice.paid",
          "data": {
            "object": {
              "id": "in_int_002",
              "object": "invoice",
              "customer": "{{customerId}}",
              "lines": {
                "data": [
                  {
                    "period": {
                      "end": {{periodEndTimestamp}}
                    }
                  }
                ]
              }
            }
          }
        }
        """;

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signature = GenerateStripeSignature(payload, WebhookSecret, timestamp);

        // Act
        var result = await _handler.Handle(new ProcessStripeWebhookCommand(payload, signature), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var updatedTenant = await _dbContext.Tenants.AsNoTracking().FirstAsync(t => t.Id == tenant.Id);
        updatedTenant.Status.Should().Be("Active");
        updatedTenant.CurrentPeriodEndUtc.Should().NotBeNull();
        updatedTenant.CurrentPeriodEndUtc!.Value.Should().BeCloseTo(
            DateTimeOffset.FromUnixTimeSeconds(periodEndTimestamp).UtcDateTime,
            TimeSpan.FromSeconds(1));

        var history = await _dbContext.SubscriptionHistories
            .FirstOrDefaultAsync(h => h.TenantId == tenant.Id && h.ActionType == SubscriptionActionType.Renewal);
        history.Should().NotBeNull();
        history!.Justification.Should().Contain("in_int_002 quitada via Stripe");
    }

    [Fact]
    public async Task Webhook_InvoicePaymentFailed_Should_SetPastDue_And_RecordHistory()
    {
        // Arrange
        var customerId = "cus_int_payment_failed_003";
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Inadimplente",
            CNPJ = "12.345.678/0001-92",
            Status = "Active",
            SubscribedPlan = "Pro",
            Capacity = 2500,
            StripeCustomerId = customerId,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        var payload = $$"""
        {
          "id": "evt_int_payment_failed_003",
          "object": "event",
          "type": "invoice.payment_failed",
          "data": {
            "object": {
              "id": "in_failed_003",
              "object": "invoice",
              "customer": "{{customerId}}"
            }
          }
        }
        """;

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signature = GenerateStripeSignature(payload, WebhookSecret, timestamp);

        // Act
        var result = await _handler.Handle(new ProcessStripeWebhookCommand(payload, signature), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var updatedTenant = await _dbContext.Tenants.AsNoTracking().FirstAsync(t => t.Id == tenant.Id);
        updatedTenant.Status.Should().Be("PastDue");
        updatedTenant.StatusReason.Should().Contain("Falha no pagamento");

        var history = await _dbContext.SubscriptionHistories
            .FirstOrDefaultAsync(h => h.TenantId == tenant.Id && h.ActionType == SubscriptionActionType.PaymentFailed);
        history.Should().NotBeNull();
        history!.Justification.Should().Contain("in_failed_003 via Stripe");
    }

    [Fact]
    public async Task Webhook_CustomerSubscriptionDeleted_Should_CancelTenant_WhenNotProtected()
    {
        // Arrange
        var subscriptionId = "sub_int_cancel_004";
        var customerId = "cus_int_cancel_004";
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Cancelamento",
            CNPJ = "12.345.678/0001-93",
            Status = "Active",
            SubscribedPlan = "Pro",
            Capacity = 2500,
            StripeCustomerId = customerId,
            StripeSubscriptionId = subscriptionId,
            IsProtected = false,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        var payload = $$"""
        {
          "id": "evt_int_sub_deleted_004",
          "object": "event",
          "type": "customer.subscription.deleted",
          "data": {
            "object": {
              "id": "{{subscriptionId}}",
              "object": "subscription",
              "customer": "{{customerId}}"
            }
          }
        }
        """;

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signature = GenerateStripeSignature(payload, WebhookSecret, timestamp);

        // Act
        var result = await _handler.Handle(new ProcessStripeWebhookCommand(payload, signature), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var updatedTenant = await _dbContext.Tenants.AsNoTracking().FirstAsync(t => t.Id == tenant.Id);
        updatedTenant.Status.Should().Be("Cancelled");
        updatedTenant.CancelAtPeriodEnd.Should().BeFalse();

        var history = await _dbContext.SubscriptionHistories
            .FirstOrDefaultAsync(h => h.TenantId == tenant.Id && h.ActionType == SubscriptionActionType.Cancelled);
        history.Should().NotBeNull();
        history!.Justification.Should().Contain("cancelada no Stripe");
    }

    [Fact]
    public async Task Webhook_CustomerSubscriptionDeleted_Should_ProtectTenant_WhenIsProtectedIsTrue()
    {
        // Arrange
        var subscriptionId = "sub_int_prot_005";
        var customerId = "cus_int_prot_005";
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Institucional Protegida",
            CNPJ = "12.345.678/0001-94",
            Status = "Active",
            SubscribedPlan = "Enterprise",
            Capacity = 10000,
            StripeCustomerId = customerId,
            StripeSubscriptionId = subscriptionId,
            IsProtected = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        var payload = $$"""
        {
          "id": "evt_int_prot_cancel_005",
          "object": "event",
          "type": "customer.subscription.deleted",
          "data": {
            "object": {
              "id": "{{subscriptionId}}",
              "object": "subscription",
              "customer": "{{customerId}}"
            }
          }
        }
        """;

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signature = GenerateStripeSignature(payload, WebhookSecret, timestamp);

        // Act
        var result = await _handler.Handle(new ProcessStripeWebhookCommand(payload, signature), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var updatedTenant = await _dbContext.Tenants.AsNoTracking().FirstAsync(t => t.Id == tenant.Id);
        updatedTenant.Status.Should().Be("Active"); // Preserved!
        updatedTenant.IsProtected.Should().BeTrue();

        var history = await _dbContext.SubscriptionHistories
            .FirstOrDefaultAsync(h => h.TenantId == tenant.Id && h.ActionType == SubscriptionActionType.Cancelled);
        history.Should().NotBeNull();
        history!.Justification.Should().Contain("ignorada devido a IsProtected=true");
    }

    [Fact]
    public async Task Webhook_DuplicateEvent_Should_BeIdempotent_And_NotDuplicateHistory()
    {
        // Arrange
        var customerId = "cus_int_idem_006";
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Idempotência",
            CNPJ = "12.345.678/0001-95",
            Status = "Active",
            SubscribedPlan = "Starter",
            Capacity = 500,
            StripeCustomerId = customerId,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        var payload = $$"""
        {
          "id": "evt_int_duplicate_006",
          "object": "event",
          "type": "invoice.paid",
          "data": {
            "object": {
              "id": "in_idem_006",
              "object": "invoice",
              "customer": "{{customerId}}"
            }
          }
        }
        """;

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signature = GenerateStripeSignature(payload, WebhookSecret, timestamp);

        // Act 1: First delivery
        var firstResult = await _handler.Handle(new ProcessStripeWebhookCommand(payload, signature), CancellationToken.None);
        firstResult.IsSuccess.Should().BeTrue();

        var historyCountAfterFirst = await _dbContext.SubscriptionHistories.CountAsync(h => h.TenantId == tenant.Id);
        historyCountAfterFirst.Should().Be(1);

        // Act 2: Second delivery (Duplicate EventId)
        var secondResult = await _handler.Handle(new ProcessStripeWebhookCommand(payload, signature), CancellationToken.None);
        secondResult.IsSuccess.Should().BeTrue();
        secondResult.Value.Message.Should().Contain("idempotente");

        // Assert: No new history record added
        var historyCountAfterSecond = await _dbContext.SubscriptionHistories.CountAsync(h => h.TenantId == tenant.Id);
        historyCountAfterSecond.Should().Be(1);
    }

    [Fact]
    public async Task Webhook_TamperedSignature_Should_FailWithInvalidSignatureError()
    {
        // Arrange
        var payload = $$"""
        {
          "id": "evt_int_tampered_007",
          "object": "event",
          "type": "invoice.paid",
          "data": {
            "object": {
              "id": "in_tampered_007",
              "object": "invoice"
            }
          }
        }
        """;

        var badSignature = "t=1234567890,v1=abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";

        // Act
        var result = await _handler.Handle(new ProcessStripeWebhookCommand(payload, badSignature), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Stripe.InvalidSignature");
    }
}
