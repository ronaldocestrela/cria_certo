using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace CriaCerto.Modules.Backoffice.UnitTests.Domain;

public class AdminSessionDomainTests
{
    [Fact]
    public void Create_WithValidParameters_ShouldCreateActiveSession()
    {
        // Arrange
        var adminUserId = Guid.NewGuid();
        var sessionToken = "session_token_123";
        var refreshToken = "refresh_token_abc";
        var ipAddress = "127.0.0.1";
        var userAgent = "Mozilla/5.0";
        var duration = TimeSpan.FromMinutes(15);
        var refreshDuration = TimeSpan.FromHours(8);

        // Act
        var session = AdminSession.Create(adminUserId, sessionToken, refreshToken, ipAddress, userAgent, duration, refreshDuration);

        // Assert
        session.Should().NotBeNull();
        session.Id.Should().NotBeEmpty();
        session.AdminUserId.Should().Be(adminUserId);
        session.SessionToken.Should().Be(sessionToken);
        session.RefreshToken.Should().Be(refreshToken);
        session.IpAddress.Should().Be(ipAddress);
        session.UserAgent.Should().Be(userAgent);
        session.IsRevoked.Should().BeFalse();
        session.IsActive().Should().BeTrue();
    }

    [Fact]
    public void Revoke_WhenSessionIsActive_ShouldMarkAsRevoked()
    {
        // Arrange
        var session = AdminSession.Create(
            Guid.NewGuid(), "st", "rt", "127.0.0.1", "Agent", TimeSpan.FromMinutes(15), TimeSpan.FromHours(8));

        // Act
        session.Revoke();

        // Assert
        session.IsRevoked.Should().BeTrue();
        session.IsActive().Should().BeFalse();
    }

    [Fact]
    public void RotateToken_WithNewTokens_ShouldUpdateTokensAndSetReplacedBy()
    {
        // Arrange
        var session = AdminSession.Create(
            Guid.NewGuid(), "st1", "rt1", "127.0.0.1", "Agent", TimeSpan.FromMinutes(15), TimeSpan.FromHours(8));
        var newSessionToken = "st2";
        var newRefreshToken = "rt2";

        // Act
        session.RotateToken(newSessionToken, newRefreshToken, TimeSpan.FromMinutes(15), TimeSpan.FromHours(8));

        // Assert
        session.SessionToken.Should().Be(newSessionToken);
        session.RefreshToken.Should().Be(newRefreshToken);
        session.ReplacedByToken.Should().Be(newRefreshToken);
        session.IsActive().Should().BeTrue();
    }
}
