using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;
using CriaCerto.Modules.Backoffice.Application.Features.AdminUsers.Commands;
using CriaCerto.Modules.Backoffice.Application.Security;
using CriaCerto.Modules.Backoffice.Infrastructure.Persistence;
using CriaCerto.Modules.Backoffice.Infrastructure.Security;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Xunit;

namespace CriaCerto.Modules.Backoffice.UnitTests.Security;

[Trait("Category", "SecurityRegression")]
public class BackofficeAuthenticationHardeningTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BackofficeDbContext _context;
    private readonly IPasswordHasherService _passwordHasher;
    private readonly IBackofficeTokenService _tokenService;
    private readonly ITotpService _totpService;

    public BackofficeAuthenticationHardeningTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<BackofficeDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new BackofficeDbContext(options);
        _context.Database.EnsureCreated();

        _passwordHasher = new PasswordHasherService();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        _tokenService = new BackofficeTokenService(config);
        _totpService = Substitute.For<ITotpService>();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Close();
        _connection.Dispose();
    }

    [Fact]
    public async Task Authenticate_WhenUserDoesNotExist_ShouldExecuteDummyPasswordVerification_ToPreventTimingAttack()
    {
        // Arrange
        var mockHasher = Substitute.For<IPasswordHasherService>();
        mockHasher.VerifyPassword(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        var handler = new AuthenticateAdminUserCommandHandler(
            _context,
            passwordHasher: mockHasher,
            tokenService: _tokenService,
            totpService: _totpService);

        var command = new AuthenticateAdminUserCommand(
            "nonexistent_admin@criacerto.com.br",
            "AnyPassword123!",
            MfaCode: null,
            IpAddress: "192.168.1.100",
            UserAgent: "SecurityScanner/1.0");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(BackofficeErrors.InvalidCredentials.Code);

        // Crucial: Verify that password verification was invoked with dummy hash to equalize processing time
        mockHasher.Received(1).VerifyPassword(
            "AnyPassword123!",
            Arg.Is<string>(hash => hash.Contains("==") && hash.Contains(".")));
    }

    [Fact]
    public async Task Authenticate_WhenPasswordIsIncorrect_ShouldFailWithInvalidCredentials()
    {
        // Arrange
        var admin = AdminUser.Create("SecOps Admin", "secops@criacerto.com.br", _passwordHasher.HashPassword("RealStrongPassword123!")).Value;
        _context.AdminUsers.Add(admin);
        await _context.SaveChangesAsync();

        var handler = new AuthenticateAdminUserCommandHandler(
            _context,
            passwordHasher: _passwordHasher,
            tokenService: _tokenService,
            totpService: _totpService);

        var command = new AuthenticateAdminUserCommand(
            "secops@criacerto.com.br",
            "WrongPassword!",
            MfaCode: null,
            IpAddress: "10.0.0.50",
            UserAgent: "Mozilla/5.0");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(BackofficeErrors.InvalidCredentials.Code);
    }

    [Fact]
    public async Task Authenticate_WhenUserIsDeactivated_ShouldFailWithUserDisabled()
    {
        // Arrange
        var admin = AdminUser.Create("Inactive Admin", "inactive@criacerto.com.br", _passwordHasher.HashPassword("Password123!")).Value;
        admin.Deactivate();
        _context.AdminUsers.Add(admin);
        await _context.SaveChangesAsync();

        var handler = new AuthenticateAdminUserCommandHandler(
            _context,
            passwordHasher: _passwordHasher,
            tokenService: _tokenService,
            totpService: _totpService);

        var command = new AuthenticateAdminUserCommand(
            "inactive@criacerto.com.br",
            "Password123!",
            MfaCode: null,
            IpAddress: "10.0.0.50",
            UserAgent: "Mozilla/5.0");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(BackofficeErrors.UserDisabled.Code);
    }

    [Fact]
    public async Task Authenticate_WhenMfaEnabledAndCodeIsMissingOrInvalid_ShouldFailSecurely()
    {
        // Arrange
        var admin = AdminUser.Create("MFA Admin", "mfa_admin@criacerto.com.br", _passwordHasher.HashPassword("Password123!")).Value;
        admin.EnableMfa("JBSWY3DPEHPK3PXP", new[] { "REC-01" });
        _context.AdminUsers.Add(admin);
        await _context.SaveChangesAsync();

        _totpService.VerifyCode("JBSWY3DPEHPK3PXP", "999999").Returns(false);
        _totpService.VerifyCode("JBSWY3DPEHPK3PXP", "123456").Returns(true);

        var handler = new AuthenticateAdminUserCommandHandler(
            _context,
            passwordHasher: _passwordHasher,
            tokenService: _tokenService,
            totpService: _totpService);

        // 1. Missing MFA Code
        var missingMfaCmd = new AuthenticateAdminUserCommand("mfa_admin@criacerto.com.br", "Password123!", null, "10.0.0.1", "Browser");
        var missingMfaResult = await handler.Handle(missingMfaCmd, CancellationToken.None);
        missingMfaResult.IsFailure.Should().BeTrue();
        missingMfaResult.Error.Code.Should().Be(BackofficeErrors.MfaRequired.Code);

        // 2. Invalid MFA Code
        var invalidMfaCmd = new AuthenticateAdminUserCommand("mfa_admin@criacerto.com.br", "Password123!", "999999", "10.0.0.1", "Browser");
        var invalidMfaResult = await handler.Handle(invalidMfaCmd, CancellationToken.None);
        invalidMfaResult.IsFailure.Should().BeTrue();
        invalidMfaResult.Error.Code.Should().Be(BackofficeErrors.InvalidMfaCode.Code);

        // 3. Valid MFA Code
        var validMfaCmd = new AuthenticateAdminUserCommand("mfa_admin@criacerto.com.br", "Password123!", "123456", "10.0.0.1", "Browser");
        var validMfaResult = await handler.Handle(validMfaCmd, CancellationToken.None);
        validMfaResult.IsSuccess.Should().BeTrue();
        validMfaResult.Value.SessionToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task RefreshSession_WhenRefreshTokenIsReplayedOrRevoked_ShouldRejectWithInvalidRefreshToken()
    {
        // Arrange
        var admin = AdminUser.Create("Session Admin", "session_admin@criacerto.com.br", "hashed_pw").Value;
        var role = AdminRole.Create(BackofficeRoles.PlatformOwner, "Owner").Value;
        admin.AssignRole(role);
        _context.AdminUsers.Add(admin);

        var sessionTokenId = Guid.NewGuid().ToString("N");
        var jwt = _tokenService.GenerateAccessToken(admin, sessionTokenId, TimeSpan.FromMinutes(30));

        var session = AdminSession.Create(
            admin.Id,
            sessionTokenId,
            "refresh_token_alpha",
            "10.0.0.1",
            "Agent/1.0",
            TimeSpan.FromMinutes(30),
            TimeSpan.FromHours(8));
        _context.AdminSessions.Add(session);
        await _context.SaveChangesAsync();

        var handler = new RefreshAdminSessionCommandHandler(_context, _tokenService);

        // Act 1: Initial Refresh succeeds and rotates token
        var firstRefreshCmd = new RefreshAdminSessionCommand(jwt, "refresh_token_alpha", "10.0.0.1", "Agent/1.0");
        var firstRefreshResult = await handler.Handle(firstRefreshCmd, CancellationToken.None);
        firstRefreshResult.IsSuccess.Should().BeTrue();

        // Act 2: Attacker replays old rotated refresh token
        var replayCmd = new RefreshAdminSessionCommand(jwt, "refresh_token_alpha", "10.0.0.1", "Agent/1.0");
        var replayResult = await handler.Handle(replayCmd, CancellationToken.None);

        // Assert: Replay attack blocked
        replayResult.IsFailure.Should().BeTrue();
        replayResult.Error.Code.Should().Be(BackofficeErrors.InvalidRefreshToken.Code);
    }

    [Fact]
    public async Task RefreshSession_WhenSessionHasExpired_ShouldRevokeAndRejectWithSessionExpired()
    {
        // Arrange
        var admin = AdminUser.Create("Exp Admin", "exp_admin@criacerto.com.br", "hashed_pw").Value;
        var role = AdminRole.Create(BackofficeRoles.PlatformOwner, "Owner").Value;
        admin.AssignRole(role);
        _context.AdminUsers.Add(admin);

        var sessionTokenId = Guid.NewGuid().ToString("N");
        var jwt = _tokenService.GenerateAccessToken(admin, sessionTokenId, TimeSpan.FromMinutes(-10));

        // Create expired session
        var session = AdminSession.Create(
            admin.Id,
            sessionTokenId,
            "refresh_token_exp",
            "10.0.0.1",
            "Agent/1.0",
            TimeSpan.FromMinutes(-10),
            TimeSpan.FromMinutes(-5));
        _context.AdminSessions.Add(session);
        await _context.SaveChangesAsync();

        var handler = new RefreshAdminSessionCommandHandler(_context, _tokenService);
        var refreshCmd = new RefreshAdminSessionCommand(jwt, "refresh_token_exp", "10.0.0.1", "Agent/1.0");

        // Act
        var result = await handler.Handle(refreshCmd, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(BackofficeErrors.SessionExpired.Code);

        // Verify session was revoked in DB
        var updatedSession = await _context.AdminSessions.FirstAsync(s => s.Id == session.Id);
        updatedSession.IsRevoked.Should().BeTrue();
    }
}
