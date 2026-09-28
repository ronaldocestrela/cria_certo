using System.Security.Cryptography;
using System.Text;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Enums;
using CriaCerto.Modules.Backoffice.Application.Features.Observability.EventHandlers;
using CriaCerto.Modules.Backoffice.Infrastructure.Persistence;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Features.ProcessStripeWebhook;
using CriaCerto.Modules.Tenancy.Application.Telemetry;
using CriaCerto.Modules.Tenancy.Infrastructure.Persistence;
using CriaCerto.Modules.Tenancy.Infrastructure.Services;
using FluentAssertions;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CriaCerto.Architecture.IntegrationTests;

public class PaymentObservabilityIntegrationTests : IDisposable
{
    private const string WebhookSecret = "whsec_test_secret_observability_2026";
    private readonly SqliteConnection _tenancyConnection;
    private readonly SqliteConnection _backofficeConnection;
    private readonly TenancyDbContext _tenancyDbContext;
    private readonly BackofficeDbContext _backofficeDbContext;
    private readonly ProcessStripeWebhookCommandHandler _handler;
    private readonly IServiceProvider _serviceProvider;

    public PaymentObservabilityIntegrationTests()
    {
        _tenancyConnection = new SqliteConnection("Filename=:memory:");
        _tenancyConnection.Open();

        var tenancyDbOptions = new DbContextOptionsBuilder<TenancyDbContext>()
            .UseSqlite(_tenancyConnection)
            .Options;

        _tenancyDbContext = new TenancyDbContext(tenancyDbOptions);
        _tenancyDbContext.Database.EnsureCreated();

        _backofficeConnection = new SqliteConnection("Filename=:memory:");
        _backofficeConnection.Open();

        var backofficeDbOptions = new DbContextOptionsBuilder<BackofficeDbContext>()
            .UseSqlite(_backofficeConnection)
            .Options;

        _backofficeDbContext = new BackofficeDbContext(backofficeDbOptions);
        _backofficeDbContext.Database.EnsureCreated();

        var services = new ServiceCollection();
        services.AddScoped<DbContext>(_ => _backofficeDbContext);
        services.AddScoped<PaymentAlertEventHandler>();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(PaymentAlertEventHandler).Assembly));
        _serviceProvider = services.BuildServiceProvider();

        var publisher = _serviceProvider.GetRequiredService<IPublisher>();

        var stripeOptions = new StripeOptions
        {
            ApiKey = "sk_test_observability_mock",
            WebhookSecret = WebhookSecret
        };

        var service = new StripePaymentService(
            _tenancyDbContext,
            Options.Create(stripeOptions),
            NullLogger<StripePaymentService>.Instance,
            publisher);

        _handler = new ProcessStripeWebhookCommandHandler(service);
    }

    public void Dispose()
    {
        _tenancyDbContext.Dispose();
        _backofficeDbContext.Dispose();
        _tenancyConnection.Close();
        _tenancyConnection.Dispose();
        _backofficeConnection.Close();
        _backofficeConnection.Dispose();
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
    public async Task Webhook_WithInvalidSignature_ShouldRecordTelemetry_And_TriggerCriticalBackofficeAlert()
    {
        // Arrange
        var payload = """
        {
          "id": "evt_invalid_sig_001",
          "object": "event",
          "type": "checkout.session.completed",
          "data": { "object": {} }
        }
        """;
        var forgedSignature = "t=1700000000,v1=bad_forged_hash_value_99999";

        // Act
        var result = await _handler.Handle(new ProcessStripeWebhookCommand(payload, forgedSignature), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Stripe.InvalidSignature");

        // Assert Backoffice Alert
        var alert = await _backofficeDbContext.Alerts
            .FirstOrDefaultAsync(a => a.RuleCode == BackofficeAlertRules.WebhookSignatureInvalid);

        alert.Should().NotBeNull();
        alert!.Severity.Should().Be(AlertSeverity.Critical);
        alert.Status.Should().Be(AlertStatus.Active);
        alert.OccurrenceCount.Should().Be(1);
        alert.Description.Should().Contain("Falha de validação criptográfica HMAC-SHA256");
    }

    [Fact]
    public async Task Webhook_InvoicePaymentFailed_ShouldMarkPastDue_And_CreateBackofficeAlertForSupport()
    {
        // Arrange
        var customerId = "cus_obs_tenant_777";
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Observabilidade Suporte",
            CNPJ = "33.444.555/0001-22",
            Status = "Active",
            SubscribedPlan = "Enterprise",
            Capacity = 3000,
            StripeCustomerId = customerId,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _tenancyDbContext.Tenants.Add(tenant);
        await _tenancyDbContext.SaveChangesAsync();

        var invoiceId = "in_obs_failed_777";
        var payload = $$"""
        {
          "id": "evt_obs_fail_001",
          "object": "event",
          "type": "invoice.payment_failed",
          "data": {
            "object": {
              "id": "{{invoiceId}}",
              "object": "invoice",
              "customer": "{{customerId}}",
              "amount_due": 59900,
              "currency": "brl"
            }
          }
        }
        """;

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signature = GenerateStripeSignature(payload, WebhookSecret, timestamp);

        // Act 1: First failed event
        var result = await _handler.Handle(new ProcessStripeWebhookCommand(payload, signature), CancellationToken.None);

        // Assert 1
        result.IsSuccess.Should().BeTrue();

        var updatedTenant = await _tenancyDbContext.Tenants.AsNoTracking().FirstAsync(t => t.Id == tenant.Id);
        updatedTenant.Status.Should().Be("PastDue");
        updatedTenant.StatusReason.Should().Contain("Falha no pagamento");

        var alert = await _backofficeDbContext.Alerts
            .FirstOrDefaultAsync(a => a.TargetTenantId == tenant.Id);

        alert.Should().NotBeNull();
        alert!.RuleCode.Should().Be(BackofficeAlertRules.PaymentInvoiceFailed);
        alert.Severity.Should().Be(AlertSeverity.Warning);
        alert.TargetTenantName.Should().Be("Fazenda Observabilidade Suporte");
        alert.OccurrenceCount.Should().Be(1);
        alert.Status.Should().Be(AlertStatus.Active);
        alert.Description.Should().Contain(invoiceId);
        alert.Description.Should().Contain("Fazenda Observabilidade Suporte");
        alert.ContextJson.Should().Contain(invoiceId);
        alert.ContextJson.Should().Contain("599");
    }
}
