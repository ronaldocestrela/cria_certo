namespace CriaCerto.Modules.Backoffice.Application.Domain.Entities;

public enum ImpersonationSessionStatus
{
    Active,
    Ended,
    Expired,
    Revoked
}

public class ImpersonationSession
{
    public const int MinDurationMinutes = 5;
    public const int MaxDurationMinutes = 60;
    public const int DefaultDurationMinutes = 15;

    public Guid Id { get; private set; }
    public Guid AdminUserId { get; private set; }
    public string AdminUserEmail { get; private set; } = default!;
    public Guid TargetTenantId { get; private set; }
    public string TargetTenantName { get; private set; } = default!;
    public Guid? TargetUserId { get; private set; }
    public string? TargetUserEmail { get; private set; }
    public string SupportTicket { get; private set; } = default!;
    public string Justification { get; private set; } = default!;
    public int DurationMinutes { get; private set; }
    public DateTime StartedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? EndedAtUtc { get; private set; }
    public ImpersonationSessionStatus Status { get; private set; }
    public string IpAddress { get; private set; } = default!;
    public string UserAgent { get; private set; } = default!;
    public string? RevocationReason { get; private set; }

    private ImpersonationSession() { }

    public static ImpersonationSession Create(
        Guid adminUserId,
        string adminUserEmail,
        Guid targetTenantId,
        string targetTenantName,
        Guid? targetUserId,
        string? targetUserEmail,
        string supportTicket,
        string justification,
        int durationMinutes,
        string ipAddress,
        string userAgent)
    {
        var clampedDuration = Math.Clamp(durationMinutes, MinDurationMinutes, MaxDurationMinutes);

        var now = DateTime.UtcNow;

        return new ImpersonationSession
        {
            Id = Guid.NewGuid(),
            AdminUserId = adminUserId,
            AdminUserEmail = adminUserEmail,
            TargetTenantId = targetTenantId,
            TargetTenantName = targetTenantName,
            TargetUserId = targetUserId,
            TargetUserEmail = targetUserEmail,
            SupportTicket = supportTicket,
            Justification = justification,
            DurationMinutes = clampedDuration,
            StartedAtUtc = now,
            ExpiresAtUtc = now.AddMinutes(clampedDuration),
            Status = ImpersonationSessionStatus.Active,
            IpAddress = ipAddress,
            UserAgent = userAgent
        };
    }

    public bool IsActive()
    {
        if (Status != ImpersonationSessionStatus.Active)
            return false;

        return DateTime.UtcNow < ExpiresAtUtc;
    }

    public int GetRemainingSeconds()
    {
        if (!IsActive())
            return 0;

        var remaining = (ExpiresAtUtc - DateTime.UtcNow).TotalSeconds;
        return remaining > 0 ? (int)remaining : 0;
    }

    public void End()
    {
        Status = ImpersonationSessionStatus.Ended;
        EndedAtUtc = DateTime.UtcNow;
    }

    public void Revoke(string? reason = null)
    {
        Status = ImpersonationSessionStatus.Revoked;
        RevocationReason = reason;
        EndedAtUtc = DateTime.UtcNow;
    }

    public void MarkAsExpired()
    {
        if (Status == ImpersonationSessionStatus.Active && DateTime.UtcNow >= ExpiresAtUtc)
        {
            Status = ImpersonationSessionStatus.Expired;
            EndedAtUtc = ExpiresAtUtc;
        }
    }
}
