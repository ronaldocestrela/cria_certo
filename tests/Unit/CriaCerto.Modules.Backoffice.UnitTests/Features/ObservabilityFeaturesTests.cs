using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Enums;
using CriaCerto.Modules.Backoffice.Application.Features.Observability.Commands;
using CriaCerto.Modules.Backoffice.Application.Features.Observability.Queries;
using CriaCerto.Modules.Backoffice.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CriaCerto.Modules.Backoffice.UnitTests.Features;

public class ObservabilityFeaturesTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BackofficeDbContext _dbContext;

    public ObservabilityFeaturesTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<BackofficeDbContext>().UseSqlite(_connection).Options;
        _dbContext = new BackofficeDbContext(options);
        _dbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Close();
        _connection.Dispose();
    }

    [Fact]
    public async Task GetOperationalHealth_NoCriticalAlerts_ShouldReturnHealthy()
    {
        // Arrange
        var handler = new GetOperationalHealthQueryHandler(_dbContext);

        // Act
        var result = await handler.Handle(new GetOperationalHealthQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(OperationalHealthStatus.Healthy);
        result.Value.ActiveCriticalAlerts.Should().Be(0);
        result.Value.IsAuditChainValid.Should().BeTrue();
    }

    [Fact]
    public async Task GetOperationalHealth_WithActiveCriticalAlert_ShouldReturnCritical()
    {
        // Arrange
        var alert = BackofficeAlert.Create(
            BackofficeAlertRules.ForensicTamperDetected,
            "Violação de Hash",
            "Cadeia quebrada",
            AlertSeverity.Critical,
            "tamper_1").Value;

        _dbContext.Alerts.Add(alert);
        await _dbContext.SaveChangesAsync();

        var handler = new GetOperationalHealthQueryHandler(_dbContext);

        // Act
        var result = await handler.Handle(new GetOperationalHealthQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(OperationalHealthStatus.Critical);
        result.Value.ActiveCriticalAlerts.Should().Be(1);
    }

    [Fact]
    public async Task GetBackofficeAlerts_WithStatusFilter_ShouldReturnFiltered()
    {
        // Arrange
        var alert1 = BackofficeAlert.Create(
            BackofficeAlertRules.PolicyBruteForce, "Alerta 1", "desc", AlertSeverity.Warning, "fp1").Value;
        var alert2 = BackofficeAlert.Create(
            BackofficeAlertRules.ImpersonationBurst, "Alerta 2", "desc", AlertSeverity.Warning, "fp2").Value;
        alert2.Resolve(Guid.NewGuid(), "admin@criacerto.com.br", "Resolvido normalmente");

        _dbContext.Alerts.AddRange(alert1, alert2);
        await _dbContext.SaveChangesAsync();

        var handler = new GetBackofficeAlertsQueryHandler(_dbContext);

        // Act
        var result = await handler.Handle(new GetBackofficeAlertsQuery(Status: AlertStatus.Active), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(1);
        result.Value.Items[0].Title.Should().Be("Alerta 1");
    }

    [Fact]
    public async Task AcknowledgeAlert_ExistingAlert_ShouldSucceed()
    {
        // Arrange
        var alert = BackofficeAlert.Create(
            BackofficeAlertRules.OffHoursCriticalAction, "Alerta", "desc", AlertSeverity.Warning, "fp1").Value;
        _dbContext.Alerts.Add(alert);
        await _dbContext.SaveChangesAsync();

        var adminId = Guid.NewGuid();
        var handler = new AcknowledgeBackofficeAlertCommandHandler(_dbContext);

        // Act
        var result = await handler.Handle(
            new AcknowledgeBackofficeAlertCommand(alert.Id, adminId, "admin@criacerto.com.br"),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var updated = await _dbContext.Alerts.FindAsync(alert.Id);
        updated!.Status.Should().Be(AlertStatus.Acknowledged);
        updated.AcknowledgedByEmail.Should().Be("admin@criacerto.com.br");
    }

    [Fact]
    public async Task ResolveAlert_ValidNotes_ShouldSucceed()
    {
        // Arrange
        var alert = BackofficeAlert.Create(
            BackofficeAlertRules.PolicyBruteForce, "Alerta", "desc", AlertSeverity.Warning, "fp1").Value;
        _dbContext.Alerts.Add(alert);
        await _dbContext.SaveChangesAsync();

        var adminId = Guid.NewGuid();
        var handler = new ResolveBackofficeAlertCommandHandler(_dbContext);

        // Act
        var result = await handler.Handle(
            new ResolveBackofficeAlertCommand(alert.Id, adminId, "admin@criacerto.com.br", "Mitigado com sucesso."),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var updated = await _dbContext.Alerts.FindAsync(alert.Id);
        updated!.Status.Should().Be(AlertStatus.Resolved);
        updated.ResolutionNotes.Should().Be("Mitigado com sucesso.");
    }

    [Fact]
    public async Task SimulateAlert_ShouldPersistAndReturnDto()
    {
        // Arrange
        var handler = new SimulateBackofficeAlertCommandHandler(_dbContext);

        // Act
        var result = await handler.Handle(
            new SimulateBackofficeAlertCommand(
                BackofficeAlertRules.SimulatedAlert,
                AlertSeverity.Info,
                "Teste Simulado",
                "Descrição do teste"),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.RuleCode.Should().Be(BackofficeAlertRules.SimulatedAlert);
        result.Value.Title.Should().Be("Teste Simulado");
        (await _dbContext.Alerts.CountAsync()).Should().Be(1);
    }
}
