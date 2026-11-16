using CriaCerto.Shared.UI.Models;

namespace CriaCerto.Shared.UI.Abstractions;

/// <summary>
/// Provedor de armazenamento durável para a fila offline de operações de sincronização.
/// Suporta persistência via IndexedDB no browser e SQLite (.NET MAUI) no ambiente mobile.
/// </summary>
public interface IOfflineSyncStorage
{
    Task InitializeAsync();
    Task<IReadOnlyList<SyncOperation>> GetPendingOperationsAsync();
    Task SavePendingOperationAsync(SyncOperation operation);
    Task RemovePendingOperationAsync(Guid operationId);
    Task ClearPendingOperationsAsync();
}

