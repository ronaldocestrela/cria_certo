using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Features.AdminUsers.Commands;
using CriaCerto.Modules.Backoffice.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CriaCerto.Modules.Backoffice.UnitTests.Features;

public class MfaAndSessionHandlersTests
{
    private (BackofficeDbContext DbContext, SqliteConnection Connection) GetInMemoryDbContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<BackofficeDbContext>()
            .UseSqlite(connection)
            .Options;

        var dbContext = new BackofficeDbContext(options);
        dbContext.Database.EnsureCreated();

        return (dbContext, connection);
    }

    [Fact]
    public async Task AuthenticateAdminUser_WithValidCredentialsNoMfa_ShouldReturnAuthResult()
    {
        // Arrange
        var (dbContext, connection) = GetInMemoryDbContext();
        using (connection)
        using (dbContext)
        {
            var user = AdminUser.Create("Support N1", "support@criacerto.com.br", "hash_Password123!").Value;
            dbContext.AdminUsers.Add(user);
            await dbContext.SaveChangesAsync();

            var handler = new AuthenticateAdminUserCommandHandler(dbContext);
            var command = new AuthenticateAdminUserCommand("support@criacerto.com.br", "Password123!", null, "127.0.0.1", "UnitTestAgent");

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.SessionToken.Should().NotBeNullOrWhiteSpace();
            result.Value.RefreshToken.Should().NotBeNullOrWhiteSpace();
            result.Value.User.Email.Should().Be("support@criacerto.com.br");

            var sessionInDb = await dbContext.AdminSessions.FirstOrDefaultAsync(s => s.AdminUserId == user.Id);
            sessionInDb.Should().NotBeNull();
            sessionInDb!.IsRevoked.Should().BeFalse();
        }
    }

    [Fact]
    public async Task AuthenticateAdminUser_WhenUserRequiresMfaAndCodeMissing_ShouldReturnMfaRequiredFailure()
    {
        // Arrange
        var (dbContext, connection) = GetInMemoryDbContext();
        using (connection)
        using (dbContext)
        {
            var user = AdminUser.Create("Platform Owner", "owner@criacerto.com.br", "hash_Password123!").Value;
            var role = AdminRole.Create("PlatformOwner", "Owner").Value;
            var perm = Permission.Create("tenants.suspend", "Suspend tenants", "Global").Value;
            role.AddPermission(perm);
            user.AssignRole(role);

            dbContext.AdminUsers.Add(user);
            await dbContext.SaveChangesAsync();

            var handler = new AuthenticateAdminUserCommandHandler(dbContext);
            var command = new AuthenticateAdminUserCommand("owner@criacerto.com.br", "Password123!", null, "127.0.0.1", "UnitTestAgent");

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Backoffice.MfaRequired");
        }
    }

    [Fact]
    public async Task RevokeAdminSession_WithValidSessionId_ShouldRevokeSession()
    {
        // Arrange
        var (dbContext, connection) = GetInMemoryDbContext();
        using (connection)
        using (dbContext)
        {
            var userId = Guid.NewGuid();
            var session = AdminSession.Create(userId, "st_123", "rt_123", "127.0.0.1", "Agent", TimeSpan.FromMinutes(30), TimeSpan.FromHours(8));
            dbContext.AdminSessions.Add(session);
            await dbContext.SaveChangesAsync();

            var handler = new RevokeAdminSessionCommandHandler(dbContext);
            var command = new RevokeAdminSessionCommand(session.Id, Guid.NewGuid(), "admin@criacerto.com.br", "127.0.0.1");

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            var sessionInDb = await dbContext.AdminSessions.FindAsync(session.Id);
            sessionInDb!.IsRevoked.Should().BeTrue();
        }
    }

    [Fact]
    public async Task RevokeAllUserSessions_WithMultipleSessions_ShouldRevokeAll()
    {
        // Arrange
        var (dbContext, connection) = GetInMemoryDbContext();
        using (connection)
        using (dbContext)
        {
            var userId = Guid.NewGuid();
            var session1 = AdminSession.Create(userId, "st_1", "rt_1", "127.0.0.1", "Agent", TimeSpan.FromMinutes(30), TimeSpan.FromHours(8));
            var session2 = AdminSession.Create(userId, "st_2", "rt_2", "127.0.0.1", "Agent", TimeSpan.FromMinutes(30), TimeSpan.FromHours(8));
            dbContext.AdminSessions.AddRange(session1, session2);
            await dbContext.SaveChangesAsync();

            var handler = new RevokeAllUserSessionsCommandHandler(dbContext);
            var command = new RevokeAllUserSessionsCommand(userId, Guid.NewGuid(), "admin@criacerto.com.br", "127.0.0.1");

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            var activeCount = await dbContext.AdminSessions.CountAsync(s => s.AdminUserId == userId && !s.IsRevoked);
            activeCount.Should().Be(0);
        }
    }
}
