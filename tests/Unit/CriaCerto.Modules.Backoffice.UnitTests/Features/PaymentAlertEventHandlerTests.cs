using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Enums;
using CriaCerto.Modules.Backoffice.Application.Features.Observability.EventHandlers;
using CriaCerto.Modules.Backoffice.Infrastructure.Persistence;
using CriaCerto.Modules.Tenancy.Application.Events;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CriaCerto.Modules.Backoffice.UnitTests.Features;

public class PaymentAlertEventHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BackofficeDbContext _dbContext;
    private readonly PaymentAlertEventHandler _handler;

    public PaymentAlertEventHandlerTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<BackofficeDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new BackofficeDbContext(options);
        _dbContext.Database.EnsureCreated();

        _handler = new PaymentAlertEventHandler(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Close();
        _connection.Dispose();
    }

    [Fact]
    public async Task Handle_PaymentInvoiceFailed_ShouldCreateAlertWithTenantContext()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var notification = new PaymentInvoiceFailedIntegrationEvent(
            TenantId: tenantId,
            TenantName: "Fazenda Estrela",
            InvoiceId: "in_test_123",
            StripeCustomerId: "cus_test_123",
            AmountDue: 450.00m,
            Currency: "BRL",
            FailureReason: "Cartão expirado",
            OccurredOnUtc: DateTime.UtcNow);

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert
        var alert = await _dbContext.Alerts.FirstOrDefaultAsync(a => a.TargetTenantId == tenantId);
        alert.Should().NotBeNull();
        alert!.RuleCode.Should().Be(BackofficeAlertRules.PaymentInvoiceFailed);
        alert.Severity.Should().Be(AlertSeverity.Warning);
        alert.TargetTenantName.Should().Be("Fazenda Estrela");
        alert.OccurrenceCount.Should().Be(1);
        alert.Status.Should().Be(AlertStatus.Active);
        alert.Description.Should().Contain("in_test_123");
        alert.Description.Should().Contain("Fazenda Estrela");
    }

    [Fact]
    public async Task Handle_PaymentInvoiceFailed_Repeated_ShouldIncrementOccurrenceCount()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var notification = new PaymentInvoiceFailedIntegrationEvent(
            TenantId: tenantId,
            TenantName: "Fazenda Estrela",
            InvoiceId: "in_test_repeated",
            StripeCustomerId: "cus_test_repeated",
            AmountDue: 450.00m,
            Currency: "BRL",
            FailureReason: "Saldo insuficiente",
            OccurredOnUtc: DateTime.UtcNow);

        // Act
        await _handler.Handle(notification, CancellationToken.None);
        await _handler.Handle(notification, CancellationToken.None);

        // Assert
        var alerts = await _dbContext.Alerts.Where(a => a.TargetTenantId == tenantId).ToListAsync();
        alerts.Should().HaveCount(1);
        alerts[0].OccurrenceCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_WebhookSignatureFailed_ShouldCreateCriticalAlert()
    {
        // Arrange
        var notification = new WebhookSignatureFailedIntegrationEvent(
            Reason: "Assinatura inválida: timestamp fora da tolerância",
            EventType: "invoice.payment_failed",
            PayloadLength: 1024,
            OccurredOnUtc: DateTime.UtcNow);

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert
        var alert = await _dbContext.Alerts
            .FirstOrDefaultAsync(a => a.RuleCode == BackofficeAlertRules.WebhookSignatureInvalid);

        alert.Should().NotBeNull();
        alert!.RuleCode.Should().Be(BackofficeAlertRules.WebhookSignatureInvalid);
        alert.Severity.Should().Be(AlertSeverity.Critical);
        alert.OccurrenceCount.Should().Be(1);
        alert.Description.Should().Contain("Assinatura inválida");
    }
}
