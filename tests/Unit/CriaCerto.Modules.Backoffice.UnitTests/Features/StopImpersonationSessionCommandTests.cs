using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Features.Impersonation.Commands;
using CriaCerto.Modules.Backoffice.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CriaCerto.Modules.Backoffice.UnitTests.Features;

public class StopImpersonationSessionCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BackofficeDbContext _dbContext;

    public StopImpersonationSessionCommandTests()
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
    public async Task Handle_WhenSessionNotFound_ShouldReturnNotFoundFailure()
    {
        // Arrange
        var handler = new StopImpersonationSessionCommandHandler(_dbContext);
        var command = new StopImpersonationSessionCommand(
            Guid.NewGuid(), Guid.NewGuid(), "admin@criacerto.com.br", "127.0.0.1");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Impersonation.SessionNotFound");
    }

    [Fact]
    public async Task Handle_WhenUnauthorizedUserAttemptsToStop_ShouldReturnUnauthorizedFailure()
    {
        // Arrange
        var creatorAdminId = Guid.NewGuid();
        var otherAdminId = Guid.NewGuid();

        var session = ImpersonationSession.Create(
            creatorAdminId, "creator@criacerto.com.br", Guid.NewGuid(), "Fazenda Boa Vista",
            null, null, "SUP-1010", "Verificação técnica.", 15, "127.0.0.1", "Agent");
        _dbContext.ImpersonationSessions.Add(session);
        await _dbContext.SaveChangesAsync();

        var handler = new StopImpersonationSessionCommandHandler(_dbContext);
        var command = new StopImpersonationSessionCommand(
            session.Id, otherAdminId, "other@criacerto.com.br", "127.0.0.1", IsPlatformOwner: false);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Impersonation.Unauthorized");
    }

    [Fact]
    public async Task Handle_WhenPlatformOwnerStopsOtherAdminSession_ShouldSucceed()
    {
        // Arrange
        var creatorAdminId = Guid.NewGuid();
        var platformOwnerId = Guid.NewGuid();

        var session = ImpersonationSession.Create(
            creatorAdminId, "creator@criacerto.com.br", Guid.NewGuid(), "Fazenda Boa Vista",
            null, null, "SUP-1010", "Verificação técnica.", 15, "127.0.0.1", "Agent");
        _dbContext.ImpersonationSessions.Add(session);
        await _dbContext.SaveChangesAsync();

        var handler = new StopImpersonationSessionCommandHandler(_dbContext);
        var command = new StopImpersonationSessionCommand(
            session.Id, platformOwnerId, "platform.owner@criacerto.com.br", "127.0.0.1", IsPlatformOwner: true);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var reloaded = await _dbContext.ImpersonationSessions.FirstAsync(s => s.Id == session.Id);
        reloaded.Status.Should().Be(ImpersonationSessionStatus.Ended);
        reloaded.EndedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WhenActiveSessionIsStopped_ShouldEndSession_AndWriteAuditLog()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var adminEmail = "support@criacerto.com.br";
        var tenantId = Guid.NewGuid();

        var session = ImpersonationSession.Create(
            adminId, adminEmail, tenantId, "Fazenda Estrela",
            null, null, "SUP-8888", "Ajuste operacional de piquetes.", 30, "10.0.0.1", "Agent");
        _dbContext.ImpersonationSessions.Add(session);
        await _dbContext.SaveChangesAsync();

        var handler = new StopImpersonationSessionCommandHandler(_dbContext);
        var command = new StopImpersonationSessionCommand(
            session.Id, adminId, adminEmail, "10.0.0.1");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var reloaded = await _dbContext.ImpersonationSessions.FirstAsync(s => s.Id == session.Id);
        reloaded.Status.Should().Be(ImpersonationSessionStatus.Ended);
        reloaded.EndedAtUtc.Should().NotBeNull();

        var audit = await _dbContext.AuditLogs.FirstOrDefaultAsync(a => a.Action == "Impersonation.Stopped");
        audit.Should().NotBeNull();
        audit!.AdminUserId.Should().Be(adminId);
        audit.Resource.Should().Be($"Tenant/{tenantId}");
        audit.DetailsJson.Should().Contain("SUP-8888");
    }
}
