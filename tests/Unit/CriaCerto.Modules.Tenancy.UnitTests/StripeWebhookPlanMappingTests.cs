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

public class StripeWebhookPlanMappingTests : IDisposable
{
    private const string WebhookSecret = "whsec_test_secret_plan_mapping_123";
    private readonly SqliteConnection _sqliteConnection;
    private readonly TenancyDbContext _dbContext;
    private readonly StripePaymentService _service;

    public StripeWebhookPlanMappingTests()
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
            ApiKey = "sk_test_123",
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
    public async Task ProcessWebhookAsync_WhenCheckoutSessionHasCanonicalPlanId_ShouldSaveCanonicalPlanAndSetProCapacity()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Plan Test",
            CNPJ = "12.345.678/0001-99",
            Status = "Trial",
            SubscribedPlan = "Starter",
            Capacity = 500
        };
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        var payload = $$"""
        {
          "id": "evt_test_checkout_1",
          "object": "event",
          "type": "checkout.session.completed",
          "data": {
            "object": {
              "id": "cs_test_session_1",
              "object": "checkout.session",
              "customer": "cus_stripe_1",
              "subscription": "sub_stripe_1",
              "payment_status": "paid",
              "metadata": {
                "TenantId": "{{tenant.Id}}",
                "PlanId": "Pro",
                "PlanName": "Pro Fazenda",
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

        var updatedTenant = await _dbContext.Tenants.FindAsync(tenant.Id);
        updatedTenant.Should().NotBeNull();
        updatedTenant!.SubscribedPlan.Should().Be("Pro");
        updatedTenant.Capacity.Should().Be(2500);
        updatedTenant.Status.Should().Be("Active");
    }

    [Fact]
    public async Task ProcessWebhookAsync_WhenCheckoutSessionHasLegacyPlanNameOnly_ShouldNormalizeToCanonicalPlan()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Legacy Test",
            CNPJ = "12.345.678/0001-98",
            Status = "Trial",
            SubscribedPlan = "Starter",
            Capacity = 500
        };
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        // Sessão legada que possuía apenas PlanName
        var payload = $$"""
        {
          "id": "evt_test_checkout_legacy",
          "object": "event",
          "type": "checkout.session.completed",
          "data": {
            "object": {
              "id": "cs_test_session_legacy",
              "object": "checkout.session",
              "customer": "cus_stripe_legacy",
              "subscription": "sub_stripe_legacy",
              "payment_status": "paid",
              "metadata": {
                "TenantId": "{{tenant.Id}}",
                "PlanName": "Pro Fazenda",
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

        var updatedTenant = await _dbContext.Tenants.FindAsync(tenant.Id);
        updatedTenant.Should().NotBeNull();
        updatedTenant!.SubscribedPlan.Should().Be("Pro");
        updatedTenant.Capacity.Should().Be(2500);
        updatedTenant.Status.Should().Be("Active");
    }

    [Fact]
    public async Task ProcessWebhookAsync_WhenCheckoutSessionHasEnterpriseCommercialName_ShouldSetEnterpriseAndCapacity()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Confinamento Grande",
            CNPJ = "12.345.678/0001-97",
            Status = "Trial",
            SubscribedPlan = "Starter",
            Capacity = 500
        };
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        var payload = $$"""
        {
          "id": "evt_test_checkout_ent",
          "object": "event",
          "type": "checkout.session.completed",
          "data": {
            "object": {
              "id": "cs_test_session_ent",
              "object": "checkout.session",
              "customer": "cus_stripe_ent",
              "subscription": "sub_stripe_ent",
              "payment_status": "paid",
              "metadata": {
                "TenantId": "{{tenant.Id}}",
                "PlanName": "Enterprise Confinamento",
                "BillingCycle": "annual"
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

        var updatedTenant = await _dbContext.Tenants.FindAsync(tenant.Id);
        updatedTenant.Should().NotBeNull();
        updatedTenant!.SubscribedPlan.Should().Be("Enterprise");
        updatedTenant.Capacity.Should().Be(100000);
        updatedTenant.Status.Should().Be("Active");
    }

    [Fact]
    public async Task ProcessWebhookAsync_WhenSubscriptionUpdatedHasPlanMetadata_ShouldSyncCanonicalPlan()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Subscription Update",
            CNPJ = "12.345.678/0001-96",
            Status = "Active",
            SubscribedPlan = "Starter",
            Capacity = 500,
            StripeCustomerId = "cus_update_1",
            StripeSubscriptionId = "sub_update_1"
        };
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        var payload = $$"""
        {
          "id": "evt_test_sub_update",
          "object": "event",
          "type": "customer.subscription.updated",
          "data": {
            "object": {
              "id": "sub_update_1",
              "object": "subscription",
              "customer": "cus_update_1",
              "status": "active",
              "cancel_at_period_end": false,
              "metadata": {
                "PlanId": "Pro",
                "PlanName": "Pro Fazenda"
              },
              "items": {
                "data": [
                  {
                    "price": {
                      "id": "price_pro_monthly"
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

        var updatedTenant = await _dbContext.Tenants.FindAsync(tenant.Id);
        updatedTenant.Should().NotBeNull();
        updatedTenant!.SubscribedPlan.Should().Be("Pro");
        updatedTenant.Capacity.Should().Be(2500);
    }

    [Fact]
    public async Task ProcessWebhookAsync_WhenSubscriptionUpdatedHasPriceIdWithoutMetadata_ShouldSyncPlanAndPeriodFromPortalChange()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Portal Update",
            CNPJ = "12.345.678/0001-95",
            Status = "Active",
            SubscribedPlan = "Starter",
            Capacity = 500,
            StripeCustomerId = "cus_update_portal_1",
            StripeSubscriptionId = "sub_update_portal_1",
            CurrentPeriodEndUtc = DateTime.UtcNow.AddDays(-10)
        };
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        var payload = $$"""
        {
          "id": "evt_test_sub_update_portal",
          "object": "event",
          "type": "customer.subscription.updated",
          "data": {
            "object": {
              "id": "sub_update_portal_1",
              "object": "subscription",
              "customer": "cus_update_portal_1",
              "status": "active",
              "cancel_at_period_end": false,
              "items": {
                "data": [
                  {
                    "current_period_end": 1893456000,
                    "price": {
                      "id": "price_enterprise_monthly"
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

        var updatedTenant = await _dbContext.Tenants.FindAsync(tenant.Id);
        updatedTenant.Should().NotBeNull();
        updatedTenant!.SubscribedPlan.Should().Be("Enterprise");
        updatedTenant.Capacity.Should().Be(100000);
        updatedTenant.CurrentPeriodEndUtc.Should().NotBeNull();
        updatedTenant.CurrentPeriodEndUtc.Should().Be(DateTime.SpecifyKind(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc), DateTimeKind.Utc));
    }
}
