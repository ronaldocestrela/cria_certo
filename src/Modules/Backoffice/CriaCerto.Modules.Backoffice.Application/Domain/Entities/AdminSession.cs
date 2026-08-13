namespace CriaCerto.Modules.Backoffice.Application.Domain.Entities;

public class AdminSession
{
    public Guid Id { get; private set; }
    public Guid AdminUserId { get; private set; }
    public string SessionToken { get; private set; } = default!;
    public string IpAddress { get; private set; } = default!;
    public string UserAgent { get; private set; } = default!;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public bool IsRevoked { get; private set; }

    private AdminSession() { }

    public static AdminSession Create(Guid adminUserId, string sessionToken, string ipAddress, string userAgent, TimeSpan duration)
    {
        return new AdminSession
        {
            Id = Guid.NewGuid(),
            AdminUserId = adminUserId,
            SessionToken = sessionToken,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.Add(duration),
            IsRevoked = false
        };
    }

    public void Revoke()
    {
        IsRevoked = true;
    }
}
