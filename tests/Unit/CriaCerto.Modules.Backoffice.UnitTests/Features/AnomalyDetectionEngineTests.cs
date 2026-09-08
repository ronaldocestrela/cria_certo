using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Enums;
using CriaCerto.Modules.Backoffice.Application.Features.Observability.Services;
using CriaCerto.Modules.Backoffice.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CriaCerto.Modules.Backoffice.UnitTests.Features;

public class AnomalyDetectionEngineTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BackofficeDbContext _dbContext;
    private readonly AnomalyDetectionEngine _engine;

    public AnomalyDetectionEngineTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<BackofficeDbContext>().UseSqlite(_connection).Options;
        _dbContext = new BackofficeDbContext(options);
        _dbContext.Database.EnsureCreated();
        _engine = new AnomalyDetectionEngine(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Close();
        _connection.Dispose();
    }

    [Fact]
    public async Task EvaluatePolicyViolations_BelowThreshold_ShouldNotTriggerAlert()
    {
        // Act
        var result = await _engine.EvaluatePolicyViolationsAsync("192.168.1.100", 4);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
        (await _dbContext.Alerts.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task EvaluatePolicyViolations_AtThreshold_ShouldCreateAlertAndIncrementOnSubsequent()
    {
        // Act 1: Trigger first time
        var result1 = await _engine.EvaluatePolicyViolationsAsync("192.168.1.100", 5);

        // Assert 1
        result1.IsSuccess.Should().BeTrue();
        result1.Value.Should().NotBeNull();
        result1.Value!.RuleCode.Should().Be(BackofficeAlertRules.PolicyBruteForce);
        result1.Value.Severity.Should().Be(AlertSeverity.Warning);
        result1.Value.OccurrenceCount.Should().Be(1);

        // Act 2: Trigger second time with same IP
        var result2 = await _engine.EvaluatePolicyViolationsAsync("192.168.1.100", 8);

        // Assert 2: Should deduplicate and increment occurrence
        result2.IsSuccess.Should().BeTrue();
        result2.Value!.OccurrenceCount.Should().Be(2);
        (await _dbContext.Alerts.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task EvaluateCriticalActionTime_DuringDay_ShouldNotTriggerAlert()
    {
        // Arrange: 14:00 BRT is 17:00 UTC on a Wednesday (2026-09-02)
        var daytimeUtc = new DateTime(2026, 9, 2, 17, 0, 0, DateTimeKind.Utc);

        // Act
        var result = await _engine.EvaluateCriticalActionTimeAsync(
            action: "Tenant.Suspend",
            auditSeverity: AuditSeverity.Critical,
            adminUserId: Guid.NewGuid(),
            adminEmail: "admin@criacerto.com.br",
            timestampUtc: daytimeUtc);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task EvaluateCriticalActionTime_NightTime_ShouldTriggerAlert()
    {
        // Arrange: 23:30 BRT is 02:30 UTC next day (2026-09-03)
        var nighttimeUtc = new DateTime(2026, 9, 3, 2, 30, 0, DateTimeKind.Utc);
        var adminId = Guid.NewGuid();

        // Act
        var result = await _engine.EvaluateCriticalActionTimeAsync(
            action: "AuditRetention.Apply",
            auditSeverity: AuditSeverity.Critical,
            adminUserId: adminId,
            adminEmail: "admin@criacerto.com.br",
            timestampUtc: nighttimeUtc);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.RuleCode.Should().Be(BackofficeAlertRules.OffHoursCriticalAction);
        result.Value.RelatedAdminUserId.Should().Be(adminId);
    }

    [Fact]
    public async Task EvaluateCriticalActionTime_Weekend_ShouldTriggerAlert()
    {
        // Arrange: Saturday afternoon (2026-09-05 18:00 UTC = 15:00 BRT)
        var weekendUtc = new DateTime(2026, 9, 5, 18, 0, 0, DateTimeKind.Utc);

        // Act
        var result = await _engine.EvaluateCriticalActionTimeAsync(
            action: "PlanVersion.Publish",
            auditSeverity: AuditSeverity.High,
            adminUserId: Guid.NewGuid(),
            adminEmail: "operator@criacerto.com.br",
            timestampUtc: weekendUtc);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.RuleCode.Should().Be(BackofficeAlertRules.OffHoursCriticalAction);
    }

    [Fact]
    public async Task EvaluateImpersonationBurst_OverThreshold_ShouldTriggerAlert()
    {
        // Arrange
        var adminId = Guid.NewGuid();

        // Act
        var result = await _engine.EvaluateImpersonationBurstAsync(adminId, "support@criacerto.com.br", 4);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.RuleCode.Should().Be(BackofficeAlertRules.ImpersonationBurst);
        result.Value.Severity.Should().Be(AlertSeverity.Warning);
    }

    [Fact]
    public async Task EvaluateAuditIntegrity_CorruptedChain_ShouldTriggerCriticalAlert()
    {
        // Act
        var result = await _engine.EvaluateAuditIntegrityAsync(
            isChainValid: false,
            corruptedRecordsCount: 2,
            details: "Hash mismatch detected on record #42");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.RuleCode.Should().Be(BackofficeAlertRules.ForensicTamperDetected);
        result.Value.Severity.Should().Be(AlertSeverity.Critical);
    }
}
