using System.Security.Cryptography;
using System.Text;
using CriaCerto.BuildingBlocks.Abstractions.Licensing;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Infrastructure.Persistence;
using CriaCerto.Modules.Tenancy.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CriaCerto.Modules.Tenancy.UnitTests;

public class StripeWebhookSubscriptionHistoryTests : IDisposable
{
    private const string WebhookSecret = "whsec_test_secret_history_123";
    private readonly SqliteConnection _sqliteConnection;
    private readonly TenancyDbContext _dbContext;
    private readonly StripePaymentService _service;

    public StripeWebhookSubscriptionHistoryTests()
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
            ApiKey = "sk_test_mock_history_123",
            WebhookSecret = WebhookSecret
        };

        _service = new StripePaymentService(
            _dbContext,
            Options.Create(stripeOptions),
            NullLogger<StripePaymentService>.Instance);
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
    public async Task ProcessWebhookAsync_WhenCheckoutSessionPaid_ShouldRecordNewSubscriptionHistory()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Santa Maria",
            CNPJ = "12.345.678/0001-01",
            Status = "Trial",
            SubscribedPlan = "Starter",
            Capacity = 500
        };
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        var payload = $$"""
        {
          "id": "evt_test_history_checkout_1",
          "object": "event",
          "type": "checkout.session.completed",
          "data": {
            "object": {
              "id": "cs_test_history_101",
              "object": "checkout.session",
              "customer": "cus_stripe_hist_1",
              "subscription": "sub_stripe_hist_1",
              "payment_status": "paid",
              "metadata": {
                "TenantId": "{{tenant.Id}}",
                "PlanId": "Pro",
                "PlanName": "Pro",
                "BillingCycle": "monthly"
              }
            }
          }
        }
        """;

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signature = GenerateStripeSignature(payload, WebhookSecret, timestamp);

        // Act
        var result = await _service.ProcessWebhookAsync(payload, signature);

        // Assert
        result.Success.Should().BeTrue();

        var histories = await _dbContext.SubscriptionHistories
            .Where(h => h.TenantId == tenant.Id)
            .ToListAsync();

        histories.Should().ContainSingle();
        var history = histories.First();
        history.ActionType.Should().Be(SubscriptionActionType.NewSubscription);
        history.ChangedByAdminUserId.Should().Be(Guid.Empty);
        history.SnapshotHeadCount.Should().Be(2500);
        history.Justification.Should().Contain("cs_test_history_101");
        history.Justification.Should().Contain("Pro");
    }

    [Fact]
    public async Task ProcessWebhookAsync_WhenInvoicePaid_ShouldRecordRenewalHistory()
    {
        // Arrange
        var customerId = "cus_stripe_hist_2";
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Boa Esperança",
            CNPJ = "12.345.678/0001-02",
            Status = "Active",
            SubscribedPlan = "Pro",
            Capacity = 2500,
            StripeCustomerId = customerId
        };
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        var payload = $$"""
        {
          "id": "evt_test_history_inv_paid_1",
          "object": "event",
          "type": "invoice.paid",
          "data": {
            "object": {
              "id": "in_test_hist_202",
              "object": "invoice",
              "customer": "{{customerId}}",
              "amount_paid": 29900,
              "lines": {
                "data": [
                  {
                    "period": {
                      "end": 1893456000
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
        var result = await _service.ProcessWebhookAsync(payload, signature);

        // Assert
        result.Success.Should().BeTrue();

        var histories = await _dbContext.SubscriptionHistories
            .Where(h => h.TenantId == tenant.Id)
            .ToListAsync();

        histories.Should().ContainSingle();
        var history = histories.First();
        history.ActionType.Should().Be(SubscriptionActionType.Renewal);
        history.ChangedByAdminUserId.Should().Be(Guid.Empty);
        history.SnapshotHeadCount.Should().Be(2500);
        history.Justification.Should().Contain("in_test_hist_202");
    }

    [Fact]
    public async Task ProcessWebhookAsync_WhenInvoicePaymentFailed_ShouldRecordPaymentFailedHistory()
    {
        // Arrange
        var customerId = "cus_stripe_hist_3";
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Inadimplente",
            CNPJ = "12.345.678/0001-03",
            Status = "Active",
            SubscribedPlan = "Enterprise",
            Capacity = 100000,
            StripeCustomerId = customerId
        };
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        var payload = $$"""
        {
          "id": "evt_test_history_inv_fail_1",
          "object": "event",
          "type": "invoice.payment_failed",
          "data": {
            "object": {
              "id": "in_test_fail_303",
              "object": "invoice",
              "customer": "{{customerId}}"
            }
          }
        }
        """;

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signature = GenerateStripeSignature(payload, WebhookSecret, timestamp);

        // Act
        var result = await _service.ProcessWebhookAsync(payload, signature);

        // Assert
        result.Success.Should().BeTrue();

        var histories = await _dbContext.SubscriptionHistories
            .Where(h => h.TenantId == tenant.Id)
            .ToListAsync();

        histories.Should().ContainSingle();
        var history = histories.First();
        history.ActionType.Should().Be(SubscriptionActionType.PaymentFailed);
        history.ChangedByAdminUserId.Should().Be(Guid.Empty);
        history.SnapshotHeadCount.Should().Be(100000);
        history.Justification.Should().Contain("in_test_fail_303");
    }

    [Fact]
    public async Task ProcessWebhookAsync_WhenSubscriptionPlanUpdated_ShouldRecordPlanChangedHistory()
    {
        // Arrange
        var customerId = "cus_stripe_hist_4";
        var subscriptionId = "sub_stripe_hist_4";
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Upgrade Portal",
            CNPJ = "12.345.678/0001-04",
            Status = "Active",
            SubscribedPlan = "Starter",
            Capacity = 500,
            StripeCustomerId = customerId,
            StripeSubscriptionId = subscriptionId
        };
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        var payload = $$"""
        {
          "id": "evt_test_history_sub_upd_1",
          "object": "event",
          "type": "customer.subscription.updated",
          "data": {
            "object": {
              "id": "{{subscriptionId}}",
              "object": "subscription",
              "customer": "{{customerId}}",
              "status": "active",
              "cancel_at_period_end": false,
              "items": {
                "data": [
                  {
                    "price": {
                      "id": "price_enterprise_monthly"
                    },
                    "current_period_end": 1893456000
                  }
                ]
              },
              "metadata": {
                "PlanId": "Enterprise"
              }
            }
          }
        }
        """;

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signature = GenerateStripeSignature(payload, WebhookSecret, timestamp);

        // Act
        var result = await _service.ProcessWebhookAsync(payload, signature);

        // Assert
        result.Success.Should().BeTrue();

        var histories = await _dbContext.SubscriptionHistories
            .Where(h => h.TenantId == tenant.Id)
            .ToListAsync();

        histories.Should().ContainSingle();
        var history = histories.First();
        history.ActionType.Should().Be(SubscriptionActionType.PlanChanged);
        history.SnapshotHeadCount.Should().Be(100000);
        history.Justification.Should().Contain("Enterprise");
    }

    [Fact]
    public async Task ProcessWebhookAsync_WhenSubscriptionDeletedForNormalTenant_ShouldRecordCancelledHistory()
    {
        // Arrange
        var customerId = "cus_stripe_hist_5";
        var subscriptionId = "sub_stripe_hist_5";
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Cancelamento",
            CNPJ = "12.345.678/0001-05",
            Status = "Active",
            SubscribedPlan = "Pro",
            Capacity = 2500,
            StripeCustomerId = customerId,
            StripeSubscriptionId = subscriptionId,
            IsProtected = false
        };
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        var payload = $$"""
        {
          "id": "evt_test_history_sub_del_1",
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
        var result = await _service.ProcessWebhookAsync(payload, signature);

        // Assert
        result.Success.Should().BeTrue();

        var histories = await _dbContext.SubscriptionHistories
            .Where(h => h.TenantId == tenant.Id)
            .ToListAsync();

        histories.Should().ContainSingle();
        var history = histories.First();
        history.ActionType.Should().Be(SubscriptionActionType.Cancelled);
        history.Justification.Should().Contain(subscriptionId);
    }

    [Fact]
    public async Task ProcessWebhookAsync_WhenSubscriptionDeletedForProtectedTenant_ShouldRecordProtectionBlockedHistory()
    {
        // Arrange
        var customerId = "cus_stripe_hist_6";
        var subscriptionId = "sub_stripe_hist_6";
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Protegida",
            CNPJ = "12.345.678/0001-06",
            Status = "Active",
            SubscribedPlan = "Pro",
            Capacity = 2500,
            StripeCustomerId = customerId,
            StripeSubscriptionId = subscriptionId,
            IsProtected = true
        };
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        var payload = $$"""
        {
          "id": "evt_test_history_sub_del_prot",
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
        var result = await _service.ProcessWebhookAsync(payload, signature);

        // Assert
        result.Success.Should().BeTrue();

        var histories = await _dbContext.SubscriptionHistories
            .Where(h => h.TenantId == tenant.Id)
            .ToListAsync();

        histories.Should().ContainSingle();
        var history = histories.First();
        history.Justification.Should().Contain("IsProtected=true");
    }

    [Fact]
    public async Task ProcessWebhookAsync_WhenDuplicateWebhookDelivered_ShouldNotDuplicateHistory()
    {
        // Arrange
        var customerId = "cus_stripe_hist_7";
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Idempotente",
            CNPJ = "12.345.678/0001-07",
            Status = "Active",
            SubscribedPlan = "Pro",
            Capacity = 2500,
            StripeCustomerId = customerId
        };
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        var payload = $$"""
        {
          "id": "evt_test_history_idempotent_1",
          "object": "event",
          "type": "invoice.paid",
          "data": {
            "object": {
              "id": "in_test_hist_idem_1",
              "object": "invoice",
              "customer": "{{customerId}}",
              "lines": {
                "data": [
                  {
                    "period": {
                      "end": 1893456000
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

        // Act - 1st delivery
        var result1 = await _service.ProcessWebhookAsync(payload, signature);
        result1.Success.Should().BeTrue();

        // Act - 2nd delivery (duplicate event)
        var result2 = await _service.ProcessWebhookAsync(payload, signature);
        result2.Success.Should().BeTrue();

        // Assert
        var count = await _dbContext.SubscriptionHistories
            .CountAsync(h => h.TenantId == tenant.Id);

        count.Should().Be(1);
    }
}
