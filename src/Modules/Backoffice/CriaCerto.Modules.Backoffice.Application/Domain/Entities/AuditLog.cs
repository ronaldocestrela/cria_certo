namespace CriaCerto.Modules.Backoffice.Application.Domain.Entities;

public class AuditLog
{
    public Guid Id { get; private set; }
    public Guid AdminUserId { get; private set; }
    public string AdminUserEmail { get; private set; } = default!;
    public string Action { get; private set; } = default!;
    public string Resource { get; private set; } = default!;
    public string IpAddress { get; private set; } = default!;
    public string? DetailsJson { get; private set; }
    public DateTime TimestampUtc { get; private set; } = DateTime.UtcNow;

    private AuditLog() { }

    public static AuditLog Create(
        Guid adminUserId,
        string adminUserEmail,
        string action,
        string resource,
        string ipAddress,
        string? detailsJson = null)
    {
        return new AuditLog
        {
            Id = Guid.NewGuid(),
            AdminUserId = adminUserId,
            AdminUserEmail = adminUserEmail,
            Action = action,
            Resource = resource,
            IpAddress = ipAddress,
            DetailsJson = detailsJson,
            TimestampUtc = DateTime.UtcNow
        };
    }
}
