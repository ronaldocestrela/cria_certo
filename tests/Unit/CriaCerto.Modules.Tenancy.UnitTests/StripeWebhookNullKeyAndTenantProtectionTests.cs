using System.Security.Cryptography;
using System.Text;
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

public class StripeWebhookNullKeyAndTenantProtectionTests : IDisposable
{
    private const string WebhookSecret = "whsec_test_secret_null_keys_123";
    private readonly SqliteConnection _sqliteConnection;
    private readonly TenancyDbContext _dbContext;
    private readonly StripePaymentService _service;

    public StripeWebhookNullKeyAndTenantProtectionTests()
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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ProcessWebhookAsync_WhenInvoicePaidHasNullOrWhitespaceCustomerId_ShouldNotUpdateAnyTenant(string? invalidCustomer)
    {
        // Arrange: Criamos um tenant sem StripeCustomerId configurado (nulo)
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Sem Stripe",
            CNPJ = "11.222.333/0001-44",
            Status = "Trial",
            SubscribedPlan = "Starter",
            Capacity = 500,
            StripeCustomerId = null,
            CurrentPeriodEndUtc = null
        };
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        var customerJsonValue = invalidCustomer == null ? "null" : $"\"{invalidCustomer}\"";
        var payload = $$"""
        {
          "id": "evt_test_invoice_paid_null_cust_{{Guid.NewGuid():N}}",
          "object": "event",
          "type": "invoice.paid",
          "data": {
            "object": {
              "id": "in_test_1",
              "object": "invoice",
              "customer": {{customerJsonValue}},
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

        var unaffectedTenant = await _dbContext.Tenants.FindAsync(tenant.Id);
        unaffectedTenant.Should().NotBeNull();
        unaffectedTenant!.Status.Should().Be("Trial");
        unaffectedTenant.CurrentPeriodEndUtc.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ProcessWebhookAsync_WhenInvoicePaymentFailedHasNullOrWhitespaceCustomerId_ShouldNotSetAnyTenantToPastDue(string? invalidCustomer)
    {
        // Arrange: Criamos um tenant ativo sem StripeCustomerId
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Ativa Sem Stripe",
            CNPJ = "22.333.444/0001-55",
            Status = "Active",
            SubscribedPlan = "Starter",
            Capacity = 500,
            StripeCustomerId = null
        };
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        var customerJsonValue = invalidCustomer == null ? "null" : $"\"{invalidCustomer}\"";
        var payload = $$"""
        {
          "id": "evt_test_invoice_failed_null_cust_{{Guid.NewGuid():N}}",
          "object": "event",
          "type": "invoice.payment_failed",
          "data": {
            "object": {
              "id": "in_test_failed_1",
              "object": "invoice",
              "customer": {{customerJsonValue}}
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

        var unaffectedTenant = await _dbContext.Tenants.FindAsync(tenant.Id);
        unaffectedTenant.Should().NotBeNull();
        unaffectedTenant!.Status.Should().Be("Active");
        unaffectedTenant.StatusReason.Should().BeNull();
    }

    [Fact]
    public async Task ProcessWebhookAsync_WhenSubscriptionUpdatedHasNullCustomerIdAndId_ShouldNotAffectAnyTenant()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Sem SubId",
            CNPJ = "33.444.555/0001-66",
            Status = "Active",
            SubscribedPlan = "Starter",
            Capacity = 500,
            StripeCustomerId = null,
            StripeSubscriptionId = null
        };
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        var payload = $$"""
        {
          "id": "evt_test_sub_updated_null_{{Guid.NewGuid():N}}",
          "object": "event",
          "type": "customer.subscription.updated",
          "data": {
            "object": {
              "id": null,
              "object": "subscription",
              "customer": null,
              "status": "canceled"
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

        var unaffectedTenant = await _dbContext.Tenants.FindAsync(tenant.Id);
        unaffectedTenant.Should().NotBeNull();
        unaffectedTenant!.Status.Should().Be("Active");
    }

    [Fact]
    public async Task ProcessWebhookAsync_WhenSubscriptionDeletedHasNullCustomerIdAndId_ShouldNotCancelAnyTenant()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Nula Test",
            CNPJ = "44.555.666/0001-77",
            Status = "Active",
            SubscribedPlan = "Starter",
            Capacity = 500,
            StripeCustomerId = null,
            StripeSubscriptionId = null
        };
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        var payload = $$"""
        {
          "id": "evt_test_sub_deleted_null_{{Guid.NewGuid():N}}",
          "object": "event",
          "type": "customer.subscription.deleted",
          "data": {
            "object": {
              "id": null,
              "object": "subscription",
              "customer": null
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

        var unaffectedTenant = await _dbContext.Tenants.FindAsync(tenant.Id);
        unaffectedTenant.Should().NotBeNull();
        unaffectedTenant!.Status.Should().Be("Active");
    }

    [Fact]
    public async Task ProcessWebhookAsync_WhenSubscriptionDeletedOnProtectedTenant_ShouldPreventCancellation()
    {
        // Arrange: Tenant protegido com IsProtected = true
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Governamental Protegida",
            CNPJ = "55.666.777/0001-88",
            Status = "Active",
            SubscribedPlan = "Enterprise",
            Capacity = 100000,
            StripeCustomerId = "cus_protected_1",
            StripeSubscriptionId = "sub_protected_1",
            IsProtected = true
        };
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        var payload = $$"""
        {
          "id": "evt_test_sub_deleted_protected_{{Guid.NewGuid():N}}",
          "object": "event",
          "type": "customer.subscription.deleted",
          "data": {
            "object": {
              "id": "{{tenant.StripeSubscriptionId}}",
              "object": "subscription",
              "customer": "{{tenant.StripeCustomerId}}"
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

        var protectedTenant = await _dbContext.Tenants.FindAsync(tenant.Id);
        protectedTenant.Should().NotBeNull();
        // Não deve ser alterado para Cancelled!
        protectedTenant!.Status.Should().Be("Active");
        protectedTenant.IsProtected.Should().BeTrue();
    }

    [Theory]
    [InlineData("canceled")]
    [InlineData("unpaid")]
    public async Task ProcessWebhookAsync_WhenSubscriptionUpdatedCanceledOrUnpaidOnProtectedTenant_ShouldPreventSuspension(string subStatus)
    {
        // Arrange: Tenant protegido com IsProtected = true
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda VIP Protegida",
            CNPJ = "66.777.888/0001-99",
            Status = "Active",
            SubscribedPlan = "Enterprise",
            Capacity = 100000,
            StripeCustomerId = "cus_vip_protected_2",
            StripeSubscriptionId = "sub_vip_protected_2",
            IsProtected = true
        };
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        var payload = $$"""
        {
          "id": "evt_test_sub_updated_protected_{{subStatus}}_{{Guid.NewGuid():N}}",
          "object": "event",
          "type": "customer.subscription.updated",
          "data": {
            "object": {
              "id": "{{tenant.StripeSubscriptionId}}",
              "object": "subscription",
              "customer": "{{tenant.StripeCustomerId}}",
              "status": "{{subStatus}}"
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

        var protectedTenant = await _dbContext.Tenants.FindAsync(tenant.Id);
        protectedTenant.Should().NotBeNull();
        // Não deve ser alterado para Suspended!
        protectedTenant!.Status.Should().Be("Active");
        protectedTenant.IsProtected.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessWebhookAsync_WhenSubscriptionDeletedOnUnprotectedTenant_ShouldCancelNormally()
    {
        // Arrange: Tenant desprotegido comum
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Desprotegida Normal",
            CNPJ = "77.888.999/0001-00",
            Status = "Active",
            SubscribedPlan = "Starter",
            Capacity = 500,
            StripeCustomerId = "cus_unprotected_3",
            StripeSubscriptionId = "sub_unprotected_3",
            IsProtected = false
        };
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        var payload = $$"""
        {
          "id": "evt_test_sub_deleted_unprotected_{{Guid.NewGuid():N}}",
          "object": "event",
          "type": "customer.subscription.deleted",
          "data": {
            "object": {
              "id": "{{tenant.StripeSubscriptionId}}",
              "object": "subscription",
              "customer": "{{tenant.StripeCustomerId}}"
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

        var cancelledTenant = await _dbContext.Tenants.FindAsync(tenant.Id);
        cancelledTenant.Should().NotBeNull();
        cancelledTenant!.Status.Should().Be("Cancelled");
        cancelledTenant.StatusReason.Should().Be("Assinatura cancelada no Stripe.");
    }
}
