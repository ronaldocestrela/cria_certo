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

public class StripeWebhookPaymentStatusTests : IDisposable
{
    private const string WebhookSecret = "whsec_test_secret_payment_status_999";
    private readonly SqliteConnection _sqliteConnection;
    private readonly TenancyDbContext _dbContext;
    private readonly StripePaymentService _service;

    public StripeWebhookPaymentStatusTests()
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
            ApiKey = "sk_test_mock_123",
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
    public async Task ProcessWebhookAsync_WhenPaymentStatusIsPaid_ShouldActivateTenantAndUpgradePlan()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Pagamento Confirmado",
            CNPJ = "12.345.678/0001-01",
            Status = "Trial",
            SubscribedPlan = "Starter",
            Capacity = 500,
            StatusReason = "Cadastro inicial de teste."
        };
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        var payload = $$"""
        {
          "id": "evt_test_paid_001",
          "object": "event",
          "type": "checkout.session.completed",
          "data": {
            "object": {
              "id": "cs_test_paid_001",
              "object": "checkout.session",
              "customer": "cus_stripe_paid_1",
              "subscription": "sub_stripe_paid_1",
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
        updatedTenant!.Status.Should().Be("Active");
        updatedTenant.StatusReason.Should().BeNull();
        updatedTenant.SubscribedPlan.Should().Be("Pro");
        updatedTenant.Capacity.Should().Be(2500);
        updatedTenant.StripeCustomerId.Should().Be("cus_stripe_paid_1");
        updatedTenant.StripeSubscriptionId.Should().Be("sub_stripe_paid_1");
        updatedTenant.StatusChangedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task ProcessWebhookAsync_WhenPaymentStatusIsUnpaid_ShouldNotActivateTenantAndSetInformativeReason()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Boleto Pendente",
            CNPJ = "12.345.678/0001-02",
            Status = "Trial",
            SubscribedPlan = "Starter",
            Capacity = 500
        };
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        var payload = $$"""
        {
          "id": "evt_test_unpaid_002",
          "object": "event",
          "type": "checkout.session.completed",
          "data": {
            "object": {
              "id": "cs_test_unpaid_002",
              "object": "checkout.session",
              "customer": "cus_stripe_unpaid_2",
              "subscription": "sub_stripe_unpaid_2",
              "payment_status": "unpaid",
              "metadata": {
                "TenantId": "{{tenant.Id}}",
                "PlanId": "Enterprise",
                "PlanName": "Enterprise Confinamento",
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
        // O status deve permanecer Trial, NÃO pode ser ativado para Active
        updatedTenant!.Status.Should().Be("Trial");
        // O plano não deve ser liberado antes do pagamento
        updatedTenant.SubscribedPlan.Should().Be("Starter");
        updatedTenant.Capacity.Should().Be(500);
        // CustomerId e SubscriptionId devem ser vinculados para permitir correlação futura com invoice.paid
        updatedTenant.StripeCustomerId.Should().Be("cus_stripe_unpaid_2");
        updatedTenant.StripeSubscriptionId.Should().Be("sub_stripe_unpaid_2");
        updatedTenant.StatusReason.Should().Contain("Aguardando confirmação de pagamento");
    }

    [Fact]
    public async Task ProcessWebhookAsync_WhenPaymentStatusIsNoPaymentRequired_ShouldActivateTenant()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Free Trial Checkout",
            CNPJ = "12.345.678/0001-03",
            Status = "Trial",
            SubscribedPlan = "Starter",
            Capacity = 500
        };
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        var payload = $$"""
        {
          "id": "evt_test_nopay_003",
          "object": "event",
          "type": "checkout.session.completed",
          "data": {
            "object": {
              "id": "cs_test_nopay_003",
              "object": "checkout.session",
              "customer": "cus_stripe_nopay_3",
              "subscription": "sub_stripe_nopay_3",
              "payment_status": "no_payment_required",
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
        updatedTenant!.Status.Should().Be("Active");
        updatedTenant.SubscribedPlan.Should().Be("Pro");
        updatedTenant.Capacity.Should().Be(2500);
    }

    [Fact]
    public async Task ProcessWebhookAsync_WhenCheckoutSessionIsUnpaidThenInvoicePaidArrives_ShouldActivateTenant()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Boleto Conciliado",
            CNPJ = "12.345.678/0001-04",
            Status = "Trial",
            SubscribedPlan = "Starter",
            Capacity = 500
        };
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        var customerId = "cus_stripe_boleto_4";

        var unpaidCheckoutPayload = $$"""
        {
          "id": "evt_test_step1_unpaid",
          "object": "event",
          "type": "checkout.session.completed",
          "data": {
            "object": {
              "id": "cs_test_step1_unpaid",
              "object": "checkout.session",
              "customer": "{{customerId}}",
              "subscription": "sub_stripe_boleto_4",
              "payment_status": "unpaid",
              "metadata": {
                "TenantId": "{{tenant.Id}}",
                "PlanId": "Pro"
              }
            }
          }
        }
        """;

        var timestamp1 = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signature1 = GenerateStripeSignature(unpaidCheckoutPayload, WebhookSecret, timestamp1);

        // Step 1: Checkout Session Unpaid
        var result1 = await _service.ProcessWebhookAsync(unpaidCheckoutPayload, signature1);
        result1.Success.Should().BeTrue();

        var tenantAfterStep1 = await _dbContext.Tenants.FindAsync(tenant.Id);
        tenantAfterStep1!.Status.Should().Be("Trial");
        tenantAfterStep1.StripeCustomerId.Should().Be(customerId);

        // Step 2: Invoice Paid arrives
        var invoicePaidPayload = $$"""
        {
          "id": "evt_test_step2_paid",
          "object": "event",
          "type": "invoice.paid",
          "data": {
            "object": {
              "id": "in_test_step2_paid",
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

        var timestamp2 = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signature2 = GenerateStripeSignature(invoicePaidPayload, WebhookSecret, timestamp2);

        var result2 = await _service.ProcessWebhookAsync(invoicePaidPayload, signature2);
        result2.Success.Should().BeTrue();

        var finalTenant = await _dbContext.Tenants.FindAsync(tenant.Id);
        finalTenant.Should().NotBeNull();
        finalTenant!.Status.Should().Be("Active");
        finalTenant.StatusReason.Should().BeNull();
        finalTenant.CurrentPeriodEndUtc.Should().NotBeNull();
    }
}
