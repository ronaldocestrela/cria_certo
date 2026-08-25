namespace CriaCerto.Modules.Tenancy.Application.Domain;

public sealed class TenantSubscriptionHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid? PreviousPlanVersionId { get; set; }
    public Guid NewPlanVersionId { get; set; }
    public Guid ChangedByAdminUserId { get; set; }
    public string Justification { get; set; } = string.Empty;
    public SubscriptionActionType ActionType { get; set; }
    public int SnapshotHeadCount { get; set; }
    public int SnapshotUserCount { get; set; }
    public int SnapshotUnitCount { get; set; }
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;

    public static TenantSubscriptionHistory Create(
        Guid tenantId,
        Guid? previousPlanVersionId,
        Guid newPlanVersionId,
        Guid changedByAdminUserId,
        string justification,
        SubscriptionActionType actionType,
        int snapshotHeadCount,
        int snapshotUserCount,
        int snapshotUnitCount)
    {
        return new TenantSubscriptionHistory
        {
            TenantId = tenantId,
            PreviousPlanVersionId = previousPlanVersionId,
            NewPlanVersionId = newPlanVersionId,
            ChangedByAdminUserId = changedByAdminUserId,
            Justification = justification,
            ActionType = actionType,
            SnapshotHeadCount = snapshotHeadCount,
            SnapshotUserCount = snapshotUserCount,
            SnapshotUnitCount = snapshotUnitCount,
            ChangedAtUtc = DateTime.UtcNow
        };
    }
}
