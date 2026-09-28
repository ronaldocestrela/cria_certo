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

public class StripeWebhookIdempotencyTests : IDisposable
{
    private const string WebhookSecret = "whsec_test_secret_idempotency_123";
    private readonly SqliteConnection _sqliteConnection;
    private readonly TenancyDbContext _dbContext;
    private readonly StripePaymentService _service;

    public StripeWebhookIdempotencyTests()
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
    public async Task ProcessWebhookAsync_WhenEventProcessedFirstTime_ShouldProcessAndPersistStripeWebhookEvent()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Idempotencia 1",
            CNPJ = "12.345.678/0001-90",
            Status = "Trial",
            SubscribedPlan = "Starter",
            Capacity = 500
        };
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        var eventId = "evt_test_idemp_101";
        var payload = $$"""
        {
          "id": "{{eventId}}",
          "object": "event",
          "type": "checkout.session.completed",
          "data": {
            "object": {
              "id": "cs_test_session_idemp_1",
              "object": "checkout.session",
              "customer": "cus_stripe_idemp_1",
              "subscription": "sub_stripe_idemp_1",
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

        // Verificar que tenant foi atualizado
        var updatedTenant = await _dbContext.Tenants.FindAsync(tenant.Id);
        updatedTenant.Should().NotBeNull();
        updatedTenant!.SubscribedPlan.Should().Be("Pro");
        updatedTenant.Status.Should().Be("Active");

        // Verificar que evento foi gravado na tabela de idempotência
        var recordedEvent = await _dbContext.StripeWebhookEvents
            .FirstOrDefaultAsync(e => e.EventId == eventId);

        recordedEvent.Should().NotBeNull();
        recordedEvent!.EventType.Should().Be("checkout.session.completed");
        recordedEvent.PayloadJson.Should().Be(payload);
        recordedEvent.ProcessedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task ProcessWebhookAsync_WhenDuplicateEventReceived_ShouldReturnSuccessWithoutReexecuting()
    {
        // Arrange
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Idempotencia Duplicada",
            CNPJ = "12.345.678/0001-91",
            Status = "Trial",
            SubscribedPlan = "Starter",
            Capacity = 500
        };
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        var eventId = "evt_test_idemp_dup_202";
        var payload = $$"""
        {
          "id": "{{eventId}}",
          "object": "event",
          "type": "checkout.session.completed",
          "data": {
            "object": {
              "id": "cs_test_session_idemp_2",
              "object": "checkout.session",
              "customer": "cus_stripe_idemp_2",
              "subscription": "sub_stripe_idemp_2",
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

        // 1ª Execução: deve processar com sucesso
        var firstResult = await _service.ProcessWebhookAsync(payload, signature);
        firstResult.Success.Should().BeTrue();

        // Simular alteração externa no tenant (ex: admin suspendeu o tenant manualmente)
        var tenantBeforeDuplicate = await _dbContext.Tenants.FindAsync(tenant.Id);
        tenantBeforeDuplicate!.Status = "Suspended";
        tenantBeforeDuplicate.SubscribedPlan = "SuspendedPlan";
        await _dbContext.SaveChangesAsync();

        // 2ª Execução (mesmo EventId reenviado pelo Stripe):
        var secondResult = await _service.ProcessWebhookAsync(payload, signature);

        // Assert: Deve retornar sucesso por idempotência
        secondResult.Success.Should().BeTrue();
        secondResult.Message.Should().Contain("idempotente");

        // O tenant NÃO deve ter sido reprocessado (deve manter o status 'Suspended' e plano inalterado)
        var tenantAfterDuplicate = await _dbContext.Tenants.FindAsync(tenant.Id);
        tenantAfterDuplicate!.Status.Should().Be("Suspended");
        tenantAfterDuplicate.SubscribedPlan.Should().Be("SuspendedPlan");

        // A tabela de idempotência deve conter exatamente 1 registro para este EventId
        var count = await _dbContext.StripeWebhookEvents.CountAsync(e => e.EventId == eventId);
        count.Should().Be(1);
    }

    [Fact]
    public async Task ProcessWebhookAsync_WhenEventPreExistsInDatabase_ShouldSkipProcessingImmediately()
    {
        // Arrange
        var eventId = "evt_pre_existing_303";
        _dbContext.StripeWebhookEvents.Add(new StripeWebhookEvent
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            EventType = "invoice.paid",
            ProcessedAtUtc = DateTime.UtcNow.AddMinutes(-10),
            PayloadJson = "{}"
        });
        await _dbContext.SaveChangesAsync();

        var payload = $$"""
        {
          "id": "{{eventId}}",
          "object": "event",
          "type": "invoice.paid",
          "data": {
            "object": {
              "id": "in_test_123",
              "object": "invoice",
              "customer": "cus_unknown"
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
        result.Message.Should().Contain("idempotente");
    }

    [Fact]
    public async Task StripeWebhookEvents_UniqueIndex_ShouldPreventDuplicateEventIds()
    {
        // Arrange
        var eventId = "evt_unique_constraint_test";
        _dbContext.StripeWebhookEvents.Add(new StripeWebhookEvent
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            EventType = "checkout.session.completed",
            ProcessedAtUtc = DateTime.UtcNow,
            PayloadJson = "{}"
        });
        await _dbContext.SaveChangesAsync();

        // Act
        _dbContext.StripeWebhookEvents.Add(new StripeWebhookEvent
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            EventType = "checkout.session.completed",
            ProcessedAtUtc = DateTime.UtcNow,
            PayloadJson = "{}"
        });

        // Assert
        var act = async () => await _dbContext.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
