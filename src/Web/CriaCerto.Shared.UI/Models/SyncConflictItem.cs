namespace CriaCerto.Shared.UI.Models;

public enum ConflictResolutionOption
{
    UseLocal = 1,
    UseServer = 2,
    Custom = 3
}

public class SyncConflictItem
{
    public Guid OperationId { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public string EntityIdentifier { get; set; } = string.Empty;
    public string LocalPayloadJson { get; set; } = string.Empty;
    public string ServerPayloadJson { get; set; } = string.Empty;
    public string ConflictReason { get; set; } = string.Empty;
    public DateTime ConflictDetectedAtUtc { get; set; } = DateTime.UtcNow;
}
