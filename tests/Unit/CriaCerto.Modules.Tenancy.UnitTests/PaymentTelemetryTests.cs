using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Text;
using CriaCerto.BuildingBlocks.Abstractions.Tenancy;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Events;
using CriaCerto.Modules.Tenancy.Application.Telemetry;
using CriaCerto.Modules.Tenancy.Infrastructure.Persistence;
using CriaCerto.Modules.Tenancy.Infrastructure.Services;
using FluentAssertions;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CriaCerto.Modules.Tenancy.UnitTests;

public class PaymentTelemetryTests : IDisposable
{
    private readonly SqliteConnection _sqliteConnection;
    private readonly TenancyDbContext _dbContext;

    public PaymentTelemetryTests()
    {
        _sqliteConnection = new SqliteConnection("Filename=:memory:");
        _sqliteConnection.Open();

        var options = new DbContextOptionsBuilder<TenancyDbContext>()
            .UseSqlite(_sqliteConnection)
            .Options;

        _dbContext = new TenancyDbContext(options);
        _dbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _sqliteConnection.Close();
        _sqliteConnection.Dispose();
    }

    [Fact]
    public void PaymentTelemetry_Instruments_ShouldBeProperlyConfigured()
    {
        PaymentTelemetry.Meter.Name.Should().Be("CriaCerto.Modules.Tenancy.Payments");
        PaymentTelemetry.Meter.Version.Should().Be("1.0.0");
        PaymentTelemetry.WebhookEventsCounter.Name.Should().Be("payments.webhook.events.total");
        PaymentTelemetry.WebhookSignatureFailuresCounter.Name.Should().Be("payments.webhook.signature_failures.total");
        PaymentTelemetry.InvoiceFailuresCounter.Name.Should().Be("payments.invoice.failures.total");
        PaymentTelemetry.WebhookProcessingDuration.Name.Should().Be("payments.webhook.duration_ms");
    }

    [Fact]
    public void PaymentTelemetry_RecordMethods_ShouldRecordDataWithoutException()
    {
        long signatureFailures = 0;
        long invoiceFailures = 0;
        long webhookEvents = 0;

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == PaymentTelemetry.MeterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            if (instrument.Name == "payments.webhook.signature_failures.total")
            {
                signatureFailures += measurement;
            }
            else if (instrument.Name == "payments.invoice.failures.total")
            {
                invoiceFailures += measurement;
            }
            else if (instrument.Name == "payments.webhook.events.total")
            {
                webhookEvents += measurement;
            }
        });

        listener.Start();

        // Act
        PaymentTelemetry.RecordSignatureFailure("invalid_signature");
        PaymentTelemetry.RecordInvoiceFailure("BRL", "insufficient_funds", 29900);
        PaymentTelemetry.RecordWebhookEvent("invoice.paid", "success");
        PaymentTelemetry.RecordWebhookDuration("invoice.paid", true, 42.5);

        listener.RecordObservableInstruments();

        // Assert
        signatureFailures.Should().BeGreaterThanOrEqualTo(1);
        invoiceFailures.Should().BeGreaterThanOrEqualTo(1);
        webhookEvents.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task StripePaymentService_WhenSignatureFails_ShouldPublishIntegrationEvent()
    {
        // Arrange
        var fakePublisher = new TestEventPublisher();
        var options = new StripeOptions
        {
            ApiKey = "sk_test_123",
            WebhookSecret = "whsec_valid_secret_12345"
        };

        var service = new StripePaymentService(
            _dbContext,
            Options.Create(options),
            NullLogger<StripePaymentService>.Instance,
            fakePublisher);

        var payload = "{\"id\":\"evt_test\",\"type\":\"invoice.paid\"}";
        var invalidSignature = "t=123456,v1=bad_signature_hash";

        // Act
        var result = await service.ProcessWebhookAsync(payload, invalidSignature);

        // Assert
        result.Success.Should().BeFalse();
        fakePublisher.PublishedEvents.Should().ContainSingle(e => e is WebhookSignatureFailedIntegrationEvent);
        var signatureEvent = fakePublisher.PublishedEvents.OfType<WebhookSignatureFailedIntegrationEvent>().First();
        signatureEvent.Reason.Should().Contain("Assinatura inválida");
        signatureEvent.PayloadLength.Should().Be(payload.Length);
    }

    [Fact]
    public async Task StripePaymentService_WhenInvoiceFails_ShouldPublishPaymentInvoiceFailedEvent()
    {
        // Arrange
        var fakePublisher = new TestEventPublisher();
        var options = new StripeOptions
        {
            ApiKey = "sk_test_123",
            WebhookSecret = "whsec_valid_secret_12345"
        };

        var service = new StripePaymentService(
            _dbContext,
            Options.Create(options),
            NullLogger<StripePaymentService>.Instance,
            fakePublisher);

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Fazenda Santa Fé",
            CNPJ = "12345678000199",
            CnpjNormalized = "12345678000199",
            SubscribedPlan = "Pro",
            StripeCustomerId = "cus_santa_fe_123",
            Status = "Active"
        };

        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = @"{
            ""id"": ""evt_fail_123"",
            ""type"": ""invoice.payment_failed"",
            ""object"": ""event"",
            ""data"": {
                ""object"": {
                    ""id"": ""in_fail_999"",
                    ""object"": ""invoice"",
                    ""customer"": ""cus_santa_fe_123"",
                    ""amount_due"": 35000,
                    ""currency"": ""brl""
                }
            }
        }";

        var signedPayload = $"{timestamp}.{payload}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(options.WebhookSecret));
        var hash = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload))).ToLowerInvariant();
        var validSignature = $"t={timestamp},v1={hash}";

        // Act
        var result = await service.ProcessWebhookAsync(payload, validSignature);

        // Assert
        result.Success.Should().BeTrue();
        fakePublisher.PublishedEvents.Should().ContainSingle(e => e is PaymentInvoiceFailedIntegrationEvent);
        var invoiceEvent = fakePublisher.PublishedEvents.OfType<PaymentInvoiceFailedIntegrationEvent>().First();
        invoiceEvent.TenantId.Should().Be(tenant.Id);
        invoiceEvent.TenantName.Should().Be(tenant.Name);
        invoiceEvent.InvoiceId.Should().Be("in_fail_999");
        invoiceEvent.AmountDue.Should().Be(350.00m);
        invoiceEvent.Currency.Should().Be("BRL");
    }

    private sealed class TestEventPublisher : IPublisher
    {
        public List<INotification> PublishedEvents { get; } = new();

        public Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            if (notification is INotification n)
            {
                PublishedEvents.Add(n);
            }
            return Task.CompletedTask;
        }

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification
        {
            PublishedEvents.Add(notification);
            return Task.CompletedTask;
        }
    }
}
