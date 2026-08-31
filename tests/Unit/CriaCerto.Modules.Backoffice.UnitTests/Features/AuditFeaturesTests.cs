using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Enums;
using CriaCerto.Modules.Backoffice.Application.Features.Audit.Commands;
using CriaCerto.Modules.Backoffice.Application.Features.Audit.Queries;
using CriaCerto.Modules.Backoffice.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CriaCerto.Modules.Backoffice.UnitTests.Features;

public class AuditFeaturesTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BackofficeDbContext _dbContext;

    public AuditFeaturesTests()
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
    public async Task GetAuditLogs_WithFilters_ShouldReturnFilteredResults()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var log1 = AuditLog.CreateForensic(
            Guid.NewGuid(), "alice@criacerto.com.br", "SupportN2", "Tenant.Suspended",
            AuditCategory.TenantManagement, AuditSeverity.Critical, "Tenant/1",
            tenantA, "Fazenda A", "192.168.1.1");

        var log2 = AuditLog.CreateForensic(
            Guid.NewGuid(), "bob@criacerto.com.br", "FinanceOps", "Billing.PaymentRecorded",
            AuditCategory.Billing, AuditSeverity.High, "Invoice/100",
            tenantB, "Fazenda B", "192.168.1.2");

        var log3 = AuditLog.Create(
            Guid.NewGuid(), "alice@criacerto.com.br", "User.Login", "User/1", "127.0.0.1");

        _dbContext.AuditLogs.AddRange(log1, log2, log3);
        await _dbContext.SaveChangesAsync();

        var handler = new GetAuditLogsQueryHandler(_dbContext);

        // Act 1: Filter by ActorEmail
        var resultAlice = await handler.Handle(new GetAuditLogsQuery(ActorEmail: "alice"), CancellationToken.None);
        resultAlice.IsSuccess.Should().BeTrue();
        resultAlice.Value.Items.Should().HaveCount(2);

        // Act 2: Filter by TargetTenantId
        var resultTenantB = await handler.Handle(new GetAuditLogsQuery(TargetTenantId: tenantB), CancellationToken.None);
        resultTenantB.IsSuccess.Should().BeTrue();
        resultTenantB.Value.Items.Should().ContainSingle(i => i.TargetTenantId == tenantB);

        // Act 3: Filter by Severity
        var resultCritical = await handler.Handle(new GetAuditLogsQuery(Severity: AuditSeverity.Critical), CancellationToken.None);
        resultCritical.IsSuccess.Should().BeTrue();
        resultCritical.Value.Items.Should().ContainSingle(i => i.Severity == AuditSeverity.Critical);
    }

    [Fact]
    public async Task GetAuditLogById_WhenExists_ShouldReturnFullDetail()
    {
        // Arrange
        var log = AuditLog.CreateForensic(
            Guid.NewGuid(), "admin@criacerto.com.br", "PlatformOwner", "PlanVersion.Published",
            AuditCategory.PlanCatalog, AuditSeverity.High, "PlanVersion/10",
            null, null, "10.0.0.1", "Mozilla/5.0",
            "{\"status\":\"Draft\"}", "{\"status\":\"Published\"}", "prev-hash-123");

        _dbContext.AuditLogs.Add(log);
        await _dbContext.SaveChangesAsync();

        var handler = new GetAuditLogByIdQueryHandler(_dbContext);

        // Act
        var result = await handler.Handle(new GetAuditLogByIdQuery(log.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(log.Id);
        result.Value.OldValuesJson.Should().Be("{\"status\":\"Draft\"}");
        result.Value.NewValuesJson.Should().Be("{\"status\":\"Published\"}");
        result.Value.PreviousRecordHash.Should().Be("prev-hash-123");
        result.Value.IsIntegrityValid.Should().BeTrue();
    }

    [Fact]
    public async Task GetAuditLogById_WhenNotFound_ShouldReturnFailure()
    {
        // Arrange
        var handler = new GetAuditLogByIdQueryHandler(_dbContext);

        // Act
        var result = await handler.Handle(new GetAuditLogByIdQuery(Guid.NewGuid()), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Audit.NotFound");
    }

    [Fact]
    public async Task GetAuditStats_ShouldReturnAggregatesAndChainStatus()
    {
        // Arrange
        var log1 = AuditLog.Create(Guid.NewGuid(), "admin@criacerto.com.br", "Tenant.Suspended", "Tenant/1", "127.0.0.1");
        var log2 = AuditLog.Create(Guid.NewGuid(), "admin@criacerto.com.br", "Billing.PaymentRecorded", "Invoice/1", "127.0.0.1");

        _dbContext.AuditLogs.AddRange(log1, log2);
        await _dbContext.SaveChangesAsync();

        var handler = new GetAuditStatsQueryHandler(_dbContext);

        // Act
        var result = await handler.Handle(new GetAuditStatsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalLogs.Should().Be(2);
        result.Value.IsChainIntegrityValid.Should().BeTrue();
        result.Value.TamperedEventsCount.Should().Be(0);
    }

    [Fact]
    public async Task VerifyAuditTrailIntegrity_WhenAllLogsValid_ShouldReportValidChain()
    {
        // Arrange
        var log1 = AuditLog.Create(Guid.NewGuid(), "admin@criacerto.com.br", "Action1", "Res/1", "127.0.0.1");
        var log2 = AuditLog.Create(Guid.NewGuid(), "admin@criacerto.com.br", "Action2", "Res/2", "127.0.0.1");

        _dbContext.AuditLogs.AddRange(log1, log2);
        await _dbContext.SaveChangesAsync();

        var handler = new VerifyAuditTrailIntegrityQueryHandler(_dbContext);

        // Act
        var result = await handler.Handle(new VerifyAuditTrailIntegrityQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsChainValid.Should().BeTrue();
        result.Value.TamperedRecordsCount.Should().Be(0);
        result.Value.ValidRecordsCount.Should().Be(2);
    }

    [Fact]
    public async Task ExportAuditTrail_ShouldReturnCsvContent()
    {
        // Arrange
        var log = AuditLog.Create(Guid.NewGuid(), "admin@criacerto.com.br", "Tenant.Created", "Tenant/1", "127.0.0.1");
        _dbContext.AuditLogs.Add(log);
        await _dbContext.SaveChangesAsync();

        var handler = new ExportAuditTrailQueryHandler(_dbContext);

        // Act
        var result = await handler.Handle(new ExportAuditTrailQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ContentType.Should().Contain("csv");
        result.Value.Content.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ApplyRetentionPolicy_DryRun_ShouldNotModifyDatabase()
    {
        // Arrange
        var oldLowLog = AuditLog.CreateForensic(
            Guid.NewGuid(), "admin@criacerto.com.br", "SupportN1", "Diagnostics.Run",
            AuditCategory.Support, AuditSeverity.Low, "Diagnostics/1",
            null, null, "127.0.0.1");

        // Force past timestamp via reflection
        typeof(AuditLog).GetProperty(nameof(AuditLog.TimestampUtc))!
            .SetValue(oldLowLog, DateTime.UtcNow.AddDays(-150));

        _dbContext.AuditLogs.Add(oldLowLog);
        await _dbContext.SaveChangesAsync();

        var handler = new ApplyAuditRetentionPolicyCommandHandler(_dbContext);

        // Act
        var result = await handler.Handle(
            new ApplyAuditRetentionPolicyCommand(Guid.NewGuid(), "admin@criacerto.com.br", "127.0.0.1", DryRun: true, LowRetentionDays: 90),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsDryRun.Should().BeTrue();
        result.Value.PurgedCount.Should().Be(1);

        // DB still has the record
        (await _dbContext.AuditLogs.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ApplyRetentionPolicy_Execution_ShouldPurgeLowLogsAndArchiveCriticalWithoutDeleting()
    {
        // Arrange
        var oldLowLog = AuditLog.CreateForensic(
            Guid.NewGuid(), "admin@criacerto.com.br", "SupportN1", "Diagnostics.Run",
            AuditCategory.Support, AuditSeverity.Low, "Diagnostics/1",
            null, null, "127.0.0.1");
        typeof(AuditLog).GetProperty(nameof(AuditLog.TimestampUtc))!
            .SetValue(oldLowLog, DateTime.UtcNow.AddDays(-150));

        var oldCriticalLog = AuditLog.CreateForensic(
            Guid.NewGuid(), "admin@criacerto.com.br", "PlatformOwner", "Tenant.Suspended",
            AuditCategory.TenantManagement, AuditSeverity.Critical, "Tenant/1",
            null, null, "127.0.0.1");
        typeof(AuditLog).GetProperty(nameof(AuditLog.TimestampUtc))!
            .SetValue(oldCriticalLog, DateTime.UtcNow.AddDays(-2000));

        _dbContext.AuditLogs.AddRange(oldLowLog, oldCriticalLog);
        await _dbContext.SaveChangesAsync();

        var handler = new ApplyAuditRetentionPolicyCommandHandler(_dbContext);

        // Act
        var result = await handler.Handle(
            new ApplyAuditRetentionPolicyCommand(Guid.NewGuid(), "admin@criacerto.com.br", "127.0.0.1", DryRun: false, CriticalRetentionDays: 1825, LowRetentionDays: 90),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PurgedCount.Should().Be(1);
        result.Value.ArchivedCount.Should().Be(1);

        // Low log is removed
        var lowInDb = await _dbContext.AuditLogs.FirstOrDefaultAsync(a => a.Id == oldLowLog.Id);
        lowInDb.Should().BeNull();

        // Critical log is NOT deleted, only archived
        var criticalInDb = await _dbContext.AuditLogs.FirstOrDefaultAsync(a => a.Id == oldCriticalLog.Id);
        criticalInDb.Should().NotBeNull();
        criticalInDb!.IsArchived.Should().BeTrue();

        // Forensic audit log of retention execution was written
        var retentionLog = await _dbContext.AuditLogs.FirstOrDefaultAsync(a => a.Action == "Audit.RetentionPolicyApplied");
        retentionLog.Should().NotBeNull();
        retentionLog!.Severity.Should().Be(AuditSeverity.Critical);
    }
}
