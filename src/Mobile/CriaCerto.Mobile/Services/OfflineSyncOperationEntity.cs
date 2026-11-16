using CriaCerto.Shared.UI.Models;
using SQLite;

namespace CriaCerto.Mobile.Services;

/// <summary>
/// Entidade de banco de dados SQLite para persistência local e durável da fila de sincronização offline.
/// </summary>
[Table("OfflineSyncOperations")]
public class OfflineSyncOperationEntity
{
    [PrimaryKey]
    public Guid Id { get; set; }

    [Indexed]
    public string ModuleName { get; set; } = string.Empty;

    public string ActionType { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = string.Empty;

    [Indexed]
    public DateTime CreatedAtUtc { get; set; }

    public int Status { get; set; }

    public string? ErrorMessage { get; set; }

    public int RetryCount { get; set; }

    public static OfflineSyncOperationEntity FromModel(SyncOperation model) => new()
    {
        Id = model.Id,
        ModuleName = model.ModuleName,
        ActionType = model.ActionType,
        PayloadJson = model.PayloadJson,
        CreatedAtUtc = model.CreatedAtUtc,
        Status = (int)model.Status,
        ErrorMessage = model.ErrorMessage,
        RetryCount = model.RetryCount
    };

    public SyncOperation ToModel() => new()
    {
        Id = Id,
        ModuleName = ModuleName,
        ActionType = ActionType,
        PayloadJson = PayloadJson,
        CreatedAtUtc = CreatedAtUtc,
        Status = (SyncOperationStatus)Status,
        ErrorMessage = ErrorMessage,
        RetryCount = RetryCount
    };
}

