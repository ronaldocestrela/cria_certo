using CriaCerto.Modules.Tenancy.Application.Domain;
using FluentAssertions;
using Xunit;

namespace CriaCerto.Modules.Tenancy.UnitTests.Domain;

public class UserDomainTests
{
    [Fact]
    public void ClearExpiredTokens_WhenTokensExpiredBeyondGracePeriod_ShouldClearTokensAndReturnCount()
    {
        // Arrange
        var now = new DateTime(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "produtor@fazenda.com.br",
            FullName = "Produtor Rural",
            EmailConfirmationToken = "expired-email-token",
            EmailConfirmationTokenExpiresAt = now.AddDays(-8), // 8 days ago (> 7 grace days)
            PasswordResetToken = "expired-reset-token",
            PasswordResetTokenExpiresAt = now.AddDays(-10) // 10 days ago (> 7 grace days)
        };

        // Act
        var cleared = user.ClearExpiredTokens(now, graceDays: 7);

        // Assert
        cleared.Should().Be(2);
        user.EmailConfirmationToken.Should().BeNull();
        user.EmailConfirmationTokenExpiresAt.Should().BeNull();
        user.PasswordResetToken.Should().BeNull();
        user.PasswordResetTokenExpiresAt.Should().BeNull();
    }

    [Fact]
    public void ClearExpiredTokens_WhenTokensExpiredWithinGracePeriod_ShouldRetainTokens()
    {
        // Arrange
        var now = new DateTime(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "produtor@fazenda.com.br",
            FullName = "Produtor Rural",
            EmailConfirmationToken = "recent-email-token",
            EmailConfirmationTokenExpiresAt = now.AddDays(-2), // 2 days ago (< 7 grace days)
            PasswordResetToken = "recent-reset-token",
            PasswordResetTokenExpiresAt = now.AddDays(-5) // 5 days ago (< 7 grace days)
        };

        // Act
        var cleared = user.ClearExpiredTokens(now, graceDays: 7);

        // Assert
        cleared.Should().Be(0);
        user.EmailConfirmationToken.Should().Be("recent-email-token");
        user.EmailConfirmationTokenExpiresAt.Should().Be(now.AddDays(-2));
        user.PasswordResetToken.Should().Be("recent-reset-token");
        user.PasswordResetTokenExpiresAt.Should().Be(now.AddDays(-5));
    }

    [Fact]
    public void ClearExpiredTokens_WhenTokensAreStillActive_ShouldRetainTokens()
    {
        // Arrange
        var now = new DateTime(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "produtor@fazenda.com.br",
            FullName = "Produtor Rural",
            EmailConfirmationToken = "active-email-token",
            EmailConfirmationTokenExpiresAt = now.AddHours(12),
            PasswordResetToken = "active-reset-token",
            PasswordResetTokenExpiresAt = now.AddHours(2)
        };

        // Act
        var cleared = user.ClearExpiredTokens(now, graceDays: 7);

        // Assert
        cleared.Should().Be(0);
        user.EmailConfirmationToken.Should().Be("active-email-token");
        user.PasswordResetToken.Should().Be("active-reset-token");
    }

    [Fact]
    public void ClearExpiredTokens_WhenOnlyOneTokenExpiredBeyondGrace_ShouldClearOnlyExpiredToken()
    {
        // Arrange
        var now = new DateTime(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "produtor@fazenda.com.br",
            FullName = "Produtor Rural",
            EmailConfirmationToken = "old-token",
            EmailConfirmationTokenExpiresAt = now.AddDays(-14), // > 7 days
            PasswordResetToken = "active-token",
            PasswordResetTokenExpiresAt = now.AddHours(5) // active
        };

        // Act
        var cleared = user.ClearExpiredTokens(now, graceDays: 7);

        // Assert
        cleared.Should().Be(1);
        user.EmailConfirmationToken.Should().BeNull();
        user.EmailConfirmationTokenExpiresAt.Should().BeNull();
        user.PasswordResetToken.Should().Be("active-token");
        user.PasswordResetTokenExpiresAt.Should().Be(now.AddHours(5));
    }
}
