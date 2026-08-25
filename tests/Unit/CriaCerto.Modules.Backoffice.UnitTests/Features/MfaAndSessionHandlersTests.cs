using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Features.AdminUsers.Commands;
using CriaCerto.Modules.Backoffice.Application.Security;
using CriaCerto.Modules.Backoffice.Infrastructure.Persistence;
using CriaCerto.Modules.Backoffice.Infrastructure.Persistence.Seeders;
using CriaCerto.Modules.Backoffice.Infrastructure.Security;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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

    private static AuthenticateAdminUserCommandHandler CreateAuthHandler(BackofficeDbContext dbContext)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        return new AuthenticateAdminUserCommandHandler(
            dbContext,
            new PasswordHasherService(),
            new BackofficeTokenService(configuration),
            new TotpService());
    }

    [Fact]
    public async Task AuthenticateAdminUser_WhenEmailNotFound_ShouldReturnInvalidCredentials()
    {
        var (dbContext, connection) = GetInMemoryDbContext();
        using (connection)
        using (dbContext)
        {
            var handler = CreateAuthHandler(dbContext);
            var command = new AuthenticateAdminUserCommand(
                "missing@criacerto.com.br",
                "AnyPassword123!",
                null,
                "127.0.0.1",
                "UnitTestAgent");

            var result = await handler.Handle(command, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Backoffice.InvalidCredentials");
        }
    }

    [Fact]
    public async Task AuthenticateAdminUser_WhenPasswordWrong_ShouldReturnInvalidCredentials()
    {
        var (dbContext, connection) = GetInMemoryDbContext();
        using (connection)
        using (dbContext)
        {
            var user = AdminUser.Create("Support N1", "support@criacerto.com.br", "hash_Password123!").Value;
            dbContext.AdminUsers.Add(user);
            await dbContext.SaveChangesAsync();

            var handler = CreateAuthHandler(dbContext);
            var command = new AuthenticateAdminUserCommand(
                "support@criacerto.com.br",
                "WrongPassword123!",
                null,
                "127.0.0.1",
                "UnitTestAgent");

            var result = await handler.Handle(command, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Backoffice.InvalidCredentials");
        }
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

            var handler = CreateAuthHandler(dbContext);
            var command = new AuthenticateAdminUserCommand("support@criacerto.com.br", "Password123!", null, "127.0.0.1", "UnitTestAgent");

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.SessionToken.Should().NotBeNullOrWhiteSpace();
            result.Value.RefreshToken.Should().NotBeNullOrWhiteSpace();
            result.Value.User.Email.Should().Be("support@criacerto.com.br");

            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Value.SessionToken);
            jwt.Claims.Should().Contain(c => c.Type == "is_backoffice_admin" && c.Value == "true");
            result.Value.SessionToken.Split('.').Should().HaveCount(3);

            var sessionInDb = await dbContext.AdminSessions.FirstOrDefaultAsync(s => s.AdminUserId == user.Id);
            sessionInDb.Should().NotBeNull();
            sessionInDb!.IsRevoked.Should().BeFalse();
            sessionInDb.SessionToken.Should().Be(jwt.Id);
            sessionInDb.SessionToken.Should().HaveLength(32);
            sessionInDb.SessionToken.Should().NotBe(result.Value.SessionToken);
        }
    }

    [Fact]
    public async Task AuthenticateAdminUser_WhenPlatformOwnerWithoutMfaEnabled_ShouldAuthenticate()
    {
        // Arrange
        var (dbContext, connection) = GetInMemoryDbContext();
        using (connection)
        using (dbContext)
        {
            var user = AdminUser.Create("Platform Owner", "owner@criacerto.com.br", "hash_Password123!").Value;
            var role = AdminRole.Create(BackofficeRoles.PlatformOwner, "Owner").Value;
            var perm = Permission.Create(BackofficePermissions.TenantsSuspend, "Suspend tenants", BackofficePermissions.ScopeGlobal).Value;
            role.AddPermission(perm);
            user.AssignRole(role);

            dbContext.AdminUsers.Add(user);
            await dbContext.SaveChangesAsync();

            var handler = CreateAuthHandler(dbContext);
            var command = new AuthenticateAdminUserCommand("owner@criacerto.com.br", "Password123!", null, "127.0.0.1", "UnitTestAgent");

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Value.SessionToken);
            jwt.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == BackofficeRoles.PlatformOwner);
            jwt.Claims.Should().Contain(c => c.Type == "is_platform_owner" && c.Value == "true");
        }
    }

    [Fact]
    public async Task AuthenticateAdminUser_WhenMfaEnabledAndCodeMissing_ShouldReturnMfaRequiredFailure()
    {
        // Arrange
        var (dbContext, connection) = GetInMemoryDbContext();
        using (connection)
        using (dbContext)
        {
            var user = AdminUser.Create("Platform Owner", "owner@criacerto.com.br", "hash_Password123!").Value;
            var role = AdminRole.Create(BackofficeRoles.PlatformOwner, "Owner").Value;
            var perm = Permission.Create(BackofficePermissions.TenantsSuspend, "Suspend tenants", BackofficePermissions.ScopeGlobal).Value;
            role.AddPermission(perm);
            user.AssignRole(role);
            user.EnableMfa("JBSWY3DPEHPK3PXP", new[] { "ABCD-1234" });

            dbContext.AdminUsers.Add(user);
            await dbContext.SaveChangesAsync();

            var handler = CreateAuthHandler(dbContext);
            var command = new AuthenticateAdminUserCommand("owner@criacerto.com.br", "Password123!", null, "127.0.0.1", "UnitTestAgent");

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Backoffice.MfaRequired");
        }
    }

    [Fact]
    public async Task AuthenticateAdminUser_WithSeededMasterAdminCredentials_ShouldReturnJwtWithPlatformOwnerRole()
    {
        // Arrange
        var (dbContext, connection) = GetInMemoryDbContext();
        using (connection)
        using (dbContext)
        {
            var passwordHasher = new PasswordHasherService();
            await BackofficeDataSeeder.SeedAsync(dbContext, passwordHasher);

            var handler = CreateAuthHandler(dbContext);
            var command = new AuthenticateAdminUserCommand(
                BackofficeDataSeeder.MasterAdminEmail,
                BackofficeDataSeeder.MasterAdminPassword,
                null,
                "127.0.0.1",
                "UnitTestAgent");

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Value.SessionToken);
            jwt.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == BackofficeRoles.PlatformOwner);
            jwt.Claims.Should().Contain(c => c.Type == "is_backoffice_admin" && c.Value == "true");
            jwt.Claims.Should().Contain(c => c.Type == "Permission" && c.Value == BackofficePermissions.UsersAdminManage);
        }
    }

    [Fact]
    public async Task RefreshAdminSession_WithJwtAndRefreshToken_ShouldRotateStoredTokenId()
    {
        // Arrange
        var (dbContext, connection) = GetInMemoryDbContext();
        using (connection)
        using (dbContext)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>())
                .Build();
            var tokenService = new BackofficeTokenService(configuration);
            var user = AdminUser.Create("Support N1", "support@criacerto.com.br", "hash_Password123!").Value;
            dbContext.AdminUsers.Add(user);
            await dbContext.SaveChangesAsync();

            var authHandler = new AuthenticateAdminUserCommandHandler(
                dbContext,
                new PasswordHasherService(),
                tokenService,
                new TotpService());
            var loginResult = await authHandler.Handle(
                new AuthenticateAdminUserCommand(
                    user.Email,
                    "Password123!",
                    null,
                    "127.0.0.1",
                    "UnitTestAgent"),
                CancellationToken.None);
            var originalTokenId = tokenService.GetTokenId(loginResult.Value.SessionToken);

            var refreshHandler = new RefreshAdminSessionCommandHandler(dbContext, tokenService);

            // Act
            var result = await refreshHandler.Handle(
                new RefreshAdminSessionCommand(
                    loginResult.Value.SessionToken,
                    loginResult.Value.RefreshToken,
                    "127.0.0.1",
                    "UnitTestAgent"),
                CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            var newTokenId = tokenService.GetTokenId(result.Value.SessionToken);
            newTokenId.Should().NotBeNullOrWhiteSpace();
            newTokenId.Should().NotBe(originalTokenId);

            var session = await dbContext.AdminSessions.SingleAsync();
            session.SessionToken.Should().Be(newTokenId);
            session.SessionToken.Should().HaveLength(32);
            session.RefreshToken.Should().Be(result.Value.RefreshToken);
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
