namespace CriaCerto.Modules.Tenancy.Application.Domain;

public sealed class TenantOperationalTag
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public Guid TagId { get; set; }
    public OperationalTag Tag { get; set; } = null!;
    public DateTime AssignedAtUtc { get; set; } = DateTime.UtcNow;
}
