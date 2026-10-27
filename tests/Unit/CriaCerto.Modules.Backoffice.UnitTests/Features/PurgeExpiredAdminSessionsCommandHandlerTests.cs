using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Enums;
using CriaCerto.Modules.Backoffice.Application.Features.DataRetention.Commands;
using CriaCerto.Modules.Backoffice.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CriaCerto.Modules.Backoffice.UnitTests.Features;

public class PurgeExpiredAdminSessionsCommandHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BackofficeDbContext _dbContext;

    public PurgeExpiredAdminSessionsCommandHandlerTests()
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
    public async Task Handle_Should_Purge_Revoked_And_Expired_Sessions_Beyond_GracePeriod()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var now = new DateTime(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);

        // 1. Sessão ativa (não revogada, expira no futuro) -> Manter
        var activeSession = AdminSession.Create(
            adminId, "token-active", "refresh-active", "127.0.0.1", "Browser",
            TimeSpan.FromHours(1), TimeSpan.FromHours(8));

        // 2. Sessão revogada há 10 dias (> 7 grace days) -> Expurgo
        var revokedOldSession = AdminSession.Create(
            adminId, "token-revoked-old", "refresh-revoked-old", "127.0.0.1", "Browser",
            TimeSpan.FromHours(-240), TimeSpan.FromHours(-200)); // 10 e 8 dias atrás
        revokedOldSession.Revoke();

        // 3. Sessão expirada há 12 dias (> 7 grace days) não revogada -> Expurgo
        var expiredOldSession = AdminSession.Create(
            adminId, "token-expired-old", "refresh-expired-old", "127.0.0.1", "Browser",
            TimeSpan.FromHours(-300), TimeSpan.FromHours(-250)); // ~10-12 dias atrás

        // 4. Sessão expirada ontem (< 7 grace days) -> Manter por estar no período de tolerância/investigação
        var recentExpiredSession = AdminSession.Create(
            adminId, "token-recent-expired", "refresh-recent-expired", "127.0.0.1", "Browser",
            TimeSpan.FromHours(-30), TimeSpan.FromHours(-20)); // ~1 dia atrás

        _dbContext.AdminSessions.AddRange(activeSession, revokedOldSession, expiredOldSession, recentExpiredSession);
        await _dbContext.SaveChangesAsync();

        var handler = new PurgeExpiredAdminSessionsCommandHandler(_dbContext);
        var command = new PurgeExpiredAdminSessionsCommand(ReferenceDateUtc: now, SessionGraceDays: 7, DryRun: false);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var summary = result.Value;
        summary.TotalPurged.Should().Be(2);
        summary.PurgedRevokedSessions.Should().Be(1);
        summary.PurgedExpiredSessions.Should().Be(1);
        summary.DryRun.Should().BeFalse();

        var remainingSessions = await _dbContext.AdminSessions.ToListAsync();
        remainingSessions.Should().HaveCount(2);
        remainingSessions.Should().Contain(s => s.Id == activeSession.Id);
        remainingSessions.Should().Contain(s => s.Id == recentExpiredSession.Id);
        remainingSessions.Should().NotContain(s => s.Id == revokedOldSession.Id);
        remainingSessions.Should().NotContain(s => s.Id == expiredOldSession.Id);

        // Verify forensic audit log created
        var audit = await _dbContext.AuditLogs
            .OrderByDescending(a => a.TimestampUtc)
            .FirstOrDefaultAsync();

        audit.Should().NotBeNull();
        audit!.Action.Should().Be("ADMIN_SESSIONS_PURGED");
        audit.Category.Should().Be(AuditCategory.Compliance);
        audit.Severity.Should().Be(AuditSeverity.Low);
        audit.DetailsJson.Should().Contain("\"TotalPurged\":2");
        audit.RecordHash.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Handle_WhenDryRun_Should_Not_Mutate_Database_And_Not_Persist_Audit()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var now = new DateTime(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);

        var staleSession = AdminSession.Create(
            adminId, "token-stale", "refresh-stale", "127.0.0.1", "Browser",
            TimeSpan.FromHours(-300), TimeSpan.FromHours(-250));
        staleSession.Revoke();

        _dbContext.AdminSessions.Add(staleSession);
        await _dbContext.SaveChangesAsync();

        var handler = new PurgeExpiredAdminSessionsCommandHandler(_dbContext);
        var command = new PurgeExpiredAdminSessionsCommand(ReferenceDateUtc: now, SessionGraceDays: 7, DryRun: true);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.DryRun.Should().BeTrue();
        result.Value.TotalPurged.Should().Be(1);

        var count = await _dbContext.AdminSessions.CountAsync();
        count.Should().Be(1);

        var auditCount = await _dbContext.AuditLogs.CountAsync();
        auditCount.Should().Be(0);
    }
}
