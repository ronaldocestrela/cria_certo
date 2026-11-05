using System.Security.Cryptography;
using System.Text;
using CriaCerto.BuildingBlocks.Abstractions.Licensing;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Features.GetSubscriptionPlans;
using CriaCerto.Modules.Tenancy.Infrastructure.Persistence;
using CriaCerto.Modules.Tenancy.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CriaCerto.Modules.Tenancy.UnitTests;

public class StripeWebhookModularSubscriptionTests : IDisposable
{
    private const string WebhookSecret = "whsec_test_modular_webhook_secret_xyz";
    private readonly SqliteConnection _sqliteConnection;
    private readonly TenancyDbContext _dbContext;
    private readonly StripePaymentService _service;

    public StripeWebhookModularSubscriptionTests()
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
            ApiKey = "sk_test_modular_123",
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
    public async Task ProcessWebhookAsync_WhenCheckoutSessionHasModularMetadata_ShouldActivateModularSubscription()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Modular 1",
            CNPJ = "12.345.678/0001-90",
            Status = "Trial",
            HeadTierCode = "TIER_500",
            Capacity = 500
        };
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        var payload = $$"""
        {
          "id": "evt_test_checkout_mod_1",
          "object": "event",
          "type": "checkout.session.completed",
          "data": {
            "object": {
              "id": "cs_test_session_modular_1",
              "object": "checkout.session",
              "customer": "cus_stripe_mod_1",
              "subscription": "sub_stripe_mod_1",
              "payment_status": "paid",
              "metadata": {
                "TenantId": "{{tenant.Id}}",
                "head_tier": "TIER_1500",
                "max_heads": "1500",
                "subscribed_modules": "Cows,Breeding,Sanitary",
                "BillingCycle": "monthly"
              }
            }
          }
        }
        """;

        var signature = GenerateStripeSignature(payload, WebhookSecret, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        // Act
        var result = await _service.ProcessWebhookAsync(payload, signature);

        // Assert
        result.Success.Should().BeTrue();

        var updatedTenant = await _dbContext.Tenants
            .Include(t => t.SubscribedModules)
            .FirstOrDefaultAsync(t => t.Id == tenant.Id);

        updatedTenant.Should().NotBeNull();
        updatedTenant!.Status.Should().Be("Active");
        updatedTenant.HeadTierCode.Should().Be("TIER_1500");
        updatedTenant.Capacity.Should().Be(1500);
        updatedTenant.StripeCustomerId.Should().Be("cus_stripe_mod_1");
        updatedTenant.StripeSubscriptionId.Should().Be("sub_stripe_mod_1");

        var moduleKeys = updatedTenant.GetSubscribedModuleKeys();
        moduleKeys.Should().Contain(["Cows", "Breeding", "Sanitary"]);
        moduleKeys.Should().HaveCount(3);

        var history = await _dbContext.SubscriptionHistories
            .Where(h => h.TenantId == tenant.Id)
            .OrderByDescending(h => h.ChangedAtUtc)
            .FirstOrDefaultAsync();

        history.Should().NotBeNull();
        history!.ActionType.Should().Be(SubscriptionActionType.NewSubscription);
        history.SnapshotHeadCount.Should().Be(1500);
        history.Justification.Should().Contain("TIER_1500");
        history.Justification.Should().Contain("Sanitary");
    }

    [Fact]
    public async Task ProcessWebhookAsync_WhenSubscriptionUpdatedAddsModule_ShouldUpdateSubscribedModulesAndRecordHistory()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Modular 2",
            CNPJ = "12.345.678/0001-91",
            Status = "Active",
            HeadTierCode = "TIER_500",
            Capacity = 500,
            StripeCustomerId = "cus_stripe_mod_2",
            StripeSubscriptionId = "sub_stripe_mod_2"
        };
        tenant.SetSubscribedModules(["Cows", "Breeding"]);
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        var payload = $$"""
        {
          "id": "evt_test_sub_update_add_mod",
          "object": "event",
          "type": "customer.subscription.updated",
          "data": {
            "object": {
              "id": "sub_stripe_mod_2",
              "object": "subscription",
              "customer": "cus_stripe_mod_2",
              "status": "active",
              "cancel_at_period_end": false,
              "metadata": {
                "TenantId": "{{tenant.Id}}",
                "head_tier": "TIER_500",
                "max_heads": "500",
                "subscribed_modules": "Cows,Breeding,Sanitary"
              }
            }
          }
        }
        """;

        var signature = GenerateStripeSignature(payload, WebhookSecret, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        // Act
        var result = await _service.ProcessWebhookAsync(payload, signature);

        // Assert
        result.Success.Should().BeTrue();

        var updatedTenant = await _dbContext.Tenants
            .Include(t => t.SubscribedModules)
            .FirstOrDefaultAsync(t => t.Id == tenant.Id);

        updatedTenant.Should().NotBeNull();
        var moduleKeys = updatedTenant!.GetSubscribedModuleKeys();
        moduleKeys.Should().Contain(["Cows", "Breeding", "Sanitary"]);
        moduleKeys.Should().HaveCount(3);

        var history = await _dbContext.SubscriptionHistories
            .Where(h => h.TenantId == tenant.Id)
            .OrderByDescending(h => h.ChangedAtUtc)
            .FirstOrDefaultAsync();

        history.Should().NotBeNull();
        history!.ActionType.Should().Be(SubscriptionActionType.PlanChanged);
        history.Justification.Should().Contain("Sanitary");
    }

    [Fact]
    public async Task ProcessWebhookAsync_WhenSubscriptionUpdatedRemovesModule_ShouldRemoveFromDatabaseAndKeepCoreCows()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Modular 3",
            CNPJ = "12.345.678/0001-92",
            Status = "Active",
            HeadTierCode = "TIER_1500",
            Capacity = 1500,
            StripeCustomerId = "cus_stripe_mod_3",
            StripeSubscriptionId = "sub_stripe_mod_3"
        };
        tenant.SetSubscribedModules(["Cows", "Breeding", "Nutrition"]);
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        // Subscribed_modules payload specifies only "Breeding", omitting "Nutrition" and omitting "Cows".
        // The service must preserve "Cows" compulsorily and remove "Nutrition".
        var payload = $$"""
        {
          "id": "evt_test_sub_update_remove_mod",
          "object": "event",
          "type": "customer.subscription.updated",
          "data": {
            "object": {
              "id": "sub_stripe_mod_3",
              "object": "subscription",
              "customer": "cus_stripe_mod_3",
              "status": "active",
              "cancel_at_period_end": false,
              "metadata": {
                "TenantId": "{{tenant.Id}}",
                "head_tier": "TIER_1500",
                "max_heads": "1500",
                "subscribed_modules": "Breeding"
              }
            }
          }
        }
        """;

        var signature = GenerateStripeSignature(payload, WebhookSecret, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        // Act
        var result = await _service.ProcessWebhookAsync(payload, signature);

        // Assert
        result.Success.Should().BeTrue();

        var updatedTenant = await _dbContext.Tenants
            .Include(t => t.SubscribedModules)
            .FirstOrDefaultAsync(t => t.Id == tenant.Id);

        updatedTenant.Should().NotBeNull();
        var moduleKeys = updatedTenant!.GetSubscribedModuleKeys();
        moduleKeys.Should().Contain(["Cows", "Breeding"]);
        moduleKeys.Should().NotContain("Nutrition");
        moduleKeys.Should().HaveCount(2);

        var history = await _dbContext.SubscriptionHistories
            .Where(h => h.TenantId == tenant.Id)
            .OrderByDescending(h => h.ChangedAtUtc)
            .FirstOrDefaultAsync();

        history.Should().NotBeNull();
        history!.Justification.Should().Contain("Nutrition");
    }

    [Fact]
    public async Task ProcessWebhookAsync_WhenSubscriptionUpdatedUpgradesHeadTier_ShouldUpdateCapacityAndRecordUpgrade()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Modular 4",
            CNPJ = "12.345.678/0001-93",
            Status = "Active",
            HeadTierCode = "TIER_500",
            Capacity = 500,
            StripeCustomerId = "cus_stripe_mod_4",
            StripeSubscriptionId = "sub_stripe_mod_4"
        };
        tenant.SetSubscribedModules(["Cows", "Breeding"]);
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        var payload = $$"""
        {
          "id": "evt_test_sub_upgrade_tier",
          "object": "event",
          "type": "customer.subscription.updated",
          "data": {
            "object": {
              "id": "sub_stripe_mod_4",
              "object": "subscription",
              "customer": "cus_stripe_mod_4",
              "status": "active",
              "cancel_at_period_end": false,
              "metadata": {
                "TenantId": "{{tenant.Id}}",
                "head_tier": "TIER_3000",
                "max_heads": "3000",
                "subscribed_modules": "Cows,Breeding"
              }
            }
          }
        }
        """;

        var signature = GenerateStripeSignature(payload, WebhookSecret, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        // Act
        var result = await _service.ProcessWebhookAsync(payload, signature);

        // Assert
        result.Success.Should().BeTrue();

        var updatedTenant = await _dbContext.Tenants
            .Include(t => t.SubscribedModules)
            .FirstOrDefaultAsync(t => t.Id == tenant.Id);

        updatedTenant.Should().NotBeNull();
        updatedTenant!.HeadTierCode.Should().Be("TIER_3000");
        updatedTenant.Capacity.Should().Be(3000);

        var history = await _dbContext.SubscriptionHistories
            .Where(h => h.TenantId == tenant.Id)
            .OrderByDescending(h => h.ChangedAtUtc)
            .FirstOrDefaultAsync();

        history.Should().NotBeNull();
        history!.ActionType.Should().Be(SubscriptionActionType.Upgrade);
        history.SnapshotHeadCount.Should().Be(3000);
        history.Justification.Should().Contain("Upgrade");
        history.Justification.Should().Contain("TIER_3000");
    }

    [Fact]
    public async Task ProcessWebhookAsync_WhenSubscriptionUpdatedDowngradesHeadTier_ShouldUpdateCapacityAndRecordDowngrade()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Modular Downgrade",
            CNPJ = "12.345.678/0001-94",
            Status = "Active",
            HeadTierCode = "TIER_3000",
            Capacity = 3000,
            StripeCustomerId = "cus_stripe_mod_dg",
            StripeSubscriptionId = "sub_stripe_mod_dg"
        };
        tenant.SetSubscribedModules(["Cows", "Breeding"]);
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        var payload = $$"""
        {
          "id": "evt_test_sub_downgrade_tier",
          "object": "event",
          "type": "customer.subscription.updated",
          "data": {
            "object": {
              "id": "sub_stripe_mod_dg",
              "object": "subscription",
              "customer": "cus_stripe_mod_dg",
              "status": "active",
              "cancel_at_period_end": false,
              "metadata": {
                "TenantId": "{{tenant.Id}}",
                "head_tier": "TIER_500",
                "max_heads": "500",
                "subscribed_modules": "Cows,Breeding"
              }
            }
          }
        }
        """;

        var signature = GenerateStripeSignature(payload, WebhookSecret, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        // Act
        var result = await _service.ProcessWebhookAsync(payload, signature);

        // Assert
        result.Success.Should().BeTrue();

        var updatedTenant = await _dbContext.Tenants
            .Include(t => t.SubscribedModules)
            .FirstOrDefaultAsync(t => t.Id == tenant.Id);

        updatedTenant.Should().NotBeNull();
        updatedTenant!.HeadTierCode.Should().Be("TIER_500");
        updatedTenant.Capacity.Should().Be(500);

        var history = await _dbContext.SubscriptionHistories
            .Where(h => h.TenantId == tenant.Id)
            .OrderByDescending(h => h.ChangedAtUtc)
            .FirstOrDefaultAsync();

        history.Should().NotBeNull();
        history!.ActionType.Should().Be(SubscriptionActionType.DowngradeImmediate);
        history.SnapshotHeadCount.Should().Be(500);
        history.Justification.Should().Contain("Downgrade");
    }

    [Fact]
    public async Task ProcessWebhookAsync_WhenSubscriptionUpdatedHasNoMetadataButModularPriceIds_ShouldResolveViaCatalog()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Modular 5",
            CNPJ = "12.345.678/0001-95",
            Status = "Active",
            HeadTierCode = "TIER_500",
            Capacity = 500,
            StripeCustomerId = "cus_stripe_mod_5",
            StripeSubscriptionId = "sub_stripe_mod_5"
        };
        tenant.SetSubscribedModules(["Cows"]);
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        // Subscription without metadata, but with items containing modular PriceIds
        var payload = $$"""
        {
          "id": "evt_test_sub_no_metadata_items",
          "object": "event",
          "type": "customer.subscription.updated",
          "data": {
            "object": {
              "id": "sub_stripe_mod_5",
              "object": "subscription",
              "customer": "cus_stripe_mod_5",
              "status": "active",
              "cancel_at_period_end": false,
              "items": {
                "object": "list",
                "data": [
                  {
                    "id": "si_item_cows_1500",
                    "price": {
                      "id": "price_cows_tier_1500_monthly"
                    }
                  },
                  {
                    "id": "si_item_sanitary_1500",
                    "price": {
                      "id": "price_sanitary_tier_1500_monthly"
                    }
                  }
                ]
              }
            }
          }
        }
        """;

        var signature = GenerateStripeSignature(payload, WebhookSecret, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        // Act
        var result = await _service.ProcessWebhookAsync(payload, signature);

        // Assert
        result.Success.Should().BeTrue();

        var updatedTenant = await _dbContext.Tenants
            .Include(t => t.SubscribedModules)
            .FirstOrDefaultAsync(t => t.Id == tenant.Id);

        updatedTenant.Should().NotBeNull();
        updatedTenant!.HeadTierCode.Should().Be("TIER_1500");
        updatedTenant.Capacity.Should().Be(1500);

        var moduleKeys = updatedTenant.GetSubscribedModuleKeys();
        moduleKeys.Should().Contain(["Cows", "Sanitary"]);
        moduleKeys.Should().HaveCount(2);

        var sanitaryModule = updatedTenant.SubscribedModules
            .FirstOrDefault(m => m.ModuleKey == "Sanitary");
        sanitaryModule.Should().NotBeNull();
        sanitaryModule!.StripeSubscriptionItemId.Should().Be("si_item_sanitary_1500");
    }

    [Fact]
    public async Task ProcessWebhookAsync_WhenInvoicePaidHasModularSubscription_ShouldExtendPeriodAndRecordRenewalHistory()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Modular 6",
            CNPJ = "12.345.678/0001-96",
            Status = "PastDue",
            HeadTierCode = "TIER_1500",
            Capacity = 1500,
            StripeCustomerId = "cus_stripe_mod_6",
            StripeSubscriptionId = "sub_stripe_mod_6"
        };
        tenant.SetSubscribedModules(["Cows", "Breeding", "Sanitary"]);
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        var renewalPeriodEnd = DateTimeOffset.UtcNow.AddMonths(1).ToUnixTimeSeconds();
        var payload = $$"""
        {
          "id": "evt_test_invoice_paid_modular",
          "object": "event",
          "type": "invoice.paid",
          "data": {
            "object": {
              "id": "in_test_mod_renewal_1",
              "object": "invoice",
              "customer": "cus_stripe_mod_6",
              "subscription": "sub_stripe_mod_6",
              "amount_paid": 31700,
              "currency": "brl",
              "period_end": {{renewalPeriodEnd}},
              "lines": {
                "object": "list",
                "data": [
                  {
                    "period": {
                      "end": {{renewalPeriodEnd}}
                    }
                  }
                ]
              }
            }
          }
        }
        """;

        var signature = GenerateStripeSignature(payload, WebhookSecret, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        // Act
        var result = await _service.ProcessWebhookAsync(payload, signature);

        // Assert
        result.Success.Should().BeTrue();

        var updatedTenant = await _dbContext.Tenants
            .Include(t => t.SubscribedModules)
            .FirstOrDefaultAsync(t => t.Id == tenant.Id);

        updatedTenant.Should().NotBeNull();
        updatedTenant!.Status.Should().Be("Active");
        updatedTenant.StatusReason.Should().BeNull();

        var history = await _dbContext.SubscriptionHistories
            .Where(h => h.TenantId == tenant.Id)
            .OrderByDescending(h => h.ChangedAtUtc)
            .FirstOrDefaultAsync();

        history.Should().NotBeNull();
        history!.ActionType.Should().Be(SubscriptionActionType.Renewal);
        history.SnapshotHeadCount.Should().Be(1500);
        history.Justification.Should().Contain("TIER_1500");
        history.Justification.Should().Contain("in_test_mod_renewal_1");
    }

    [Fact]
    public async Task ProcessWebhookAsync_WhenDuplicateModularWebhookDelivered_ShouldBeIdempotent()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Modular 7",
            CNPJ = "12.345.678/0001-97",
            Status = "Active",
            HeadTierCode = "TIER_500",
            Capacity = 500,
            StripeCustomerId = "cus_stripe_mod_7",
            StripeSubscriptionId = "sub_stripe_mod_7"
        };
        tenant.SetSubscribedModules(["Cows"]);
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        var payload = $$"""
        {
          "id": "evt_test_modular_duplicate_1",
          "object": "event",
          "type": "customer.subscription.updated",
          "data": {
            "object": {
              "id": "sub_stripe_mod_7",
              "object": "subscription",
              "customer": "cus_stripe_mod_7",
              "status": "active",
              "cancel_at_period_end": false,
              "metadata": {
                "TenantId": "{{tenant.Id}}",
                "head_tier": "TIER_500",
                "max_heads": "500",
                "subscribed_modules": "Cows,Calving"
              }
            }
          }
        }
        """;

        var signature = GenerateStripeSignature(payload, WebhookSecret, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        // Act
        var result1 = await _service.ProcessWebhookAsync(payload, signature);
        var result2 = await _service.ProcessWebhookAsync(payload, signature);

        // Assert
        result1.Success.Should().BeTrue();
        result2.Success.Should().BeTrue();
        result2.Message.Should().Contain("idempotente");

        var modulesCount = await _dbContext.TenantSubscribedModules
            .CountAsync(m => m.TenantId == tenant.Id && m.ModuleKey == "Calving");
        modulesCount.Should().Be(1);

        var historyCount = await _dbContext.SubscriptionHistories
            .CountAsync(h => h.TenantId == tenant.Id);
        historyCount.Should().Be(1);
    }
}

