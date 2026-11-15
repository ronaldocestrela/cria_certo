using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Shared.UI.Models;

namespace CriaCerto.Shared.UI.Services;

public interface IOfflineSyncService
{
    bool IsOnline { get; }
    bool IsSyncing { get; }
    int PendingCount { get; }
    IReadOnlyList<SyncOperation> PendingOperations { get; }
    IReadOnlyList<SyncConflictItem> ActiveConflicts { get; }

    event Action? OnStateChanged;

    Task InitializeAsync();
    Task<Result> EnqueueOperationAsync<T>(string moduleName, string actionType, T payload);
    Task<Result> ForceSyncAsync();
    Task<Result> ResolveConflictAsync(Guid operationId, ConflictResolutionOption resolution);
    Task<Result> ClearPendingOperationsAsync();
    void SetNetworkStatus(bool isOnline);
}
