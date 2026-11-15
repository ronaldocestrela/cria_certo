using System.Text.Json.Serialization;

namespace CriaCerto.Shared.UI.Models;

public enum SyncOperationStatus
{
    Pending = 0,
    Syncing = 1,
    Success = 2,
    Conflict = 3,
    Failed = 4
}

public class SyncOperation
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [JsonPropertyName("moduleName")]
    public string ModuleName { get; set; } = string.Empty;

    [JsonPropertyName("actionType")]
    public string ActionType { get; set; } = string.Empty;

    [JsonPropertyName("payloadJson")]
    public string PayloadJson { get; set; } = string.Empty;

    [JsonPropertyName("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("status")]
    public SyncOperationStatus Status { get; set; } = SyncOperationStatus.Pending;

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("retryCount")]
    public int RetryCount { get; set; } = 0;
}
