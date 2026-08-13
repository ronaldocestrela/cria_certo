namespace CriaCerto.Modules.Backoffice.Application.Domain.Entities;

public class AdminSession
{
    public Guid Id { get; private set; }
    public Guid AdminUserId { get; private set; }
    public string SessionToken { get; private set; } = default!;
    public string RefreshToken { get; private set; } = default!;
    public string IpAddress { get; private set; } = default!;
    public string UserAgent { get; private set; } = default!;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime RefreshTokenExpiresAtUtc { get; private set; }
    public bool IsRevoked { get; private set; }
    public string? ReplacedByToken { get; private set; }

    private AdminSession() { }

    public static AdminSession Create(
        Guid adminUserId,
        string sessionToken,
        string refreshToken,
        string ipAddress,
        string userAgent,
        TimeSpan sessionDuration,
        TimeSpan refreshDuration)
    {
        var now = DateTime.UtcNow;
        return new AdminSession
        {
            Id = Guid.NewGuid(),
            AdminUserId = adminUserId,
            SessionToken = sessionToken,
            RefreshToken = refreshToken,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.Add(sessionDuration),
            RefreshTokenExpiresAtUtc = now.Add(refreshDuration),
            IsRevoked = false
        };
    }

    public static AdminSession Create(Guid adminUserId, string sessionToken, string ipAddress, string userAgent, TimeSpan duration)
    {
        return Create(adminUserId, sessionToken, sessionToken, ipAddress, userAgent, duration, TimeSpan.FromHours(8));
    }

    public bool IsActive()
    {
        return !IsRevoked && DateTime.UtcNow < RefreshTokenExpiresAtUtc;
    }

    public void RotateToken(string newSessionToken, string newRefreshToken, TimeSpan sessionDuration, TimeSpan refreshDuration)
    {
        ReplacedByToken = newRefreshToken;
        SessionToken = newSessionToken;
        RefreshToken = newRefreshToken;
        var now = DateTime.UtcNow;
        ExpiresAtUtc = now.Add(sessionDuration);
        RefreshTokenExpiresAtUtc = now.Add(refreshDuration);
    }

    public void Revoke()
    {
        IsRevoked = true;
    }
}
