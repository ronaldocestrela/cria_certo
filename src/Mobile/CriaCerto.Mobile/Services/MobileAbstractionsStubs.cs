using CriaCerto.Shared.UI.Abstractions;
using CriaCerto.Shared.UI.Models;

namespace CriaCerto.Mobile.Services;

/// <summary>
/// Implementação inicial de armazenamento seguro de tokens para MAUI (Fase 3).
/// Na Fase 4 será integrada com SecureStorage.Default com fallback para simuladores.
/// </summary>
public class MobileTokenStorageStub : ITokenStorageService
{
    private string? _cachedToken;

    public Task<string?> GetTokenAsync() => Task.FromResult(_cachedToken);

    public Task SetTokenAsync(string token)
    {
        _cachedToken = token;
        return Task.CompletedTask;
    }

    public Task RemoveTokenAsync()
    {
        _cachedToken = null;
        return Task.CompletedTask;
    }
}

/// <summary>
/// Implementação inicial de exportação e compartilhamento de arquivos para MAUI (Fase 3).
/// Na Fase 4 será integrada nativamente com Share.Default e FileSystem.CacheDirectory.
/// </summary>
public class MobileFileExportStub : IFileExportService
{
    public Task ExportFileAsync(string fileName, string contentType, byte[] content)
    {
        // Placeholder Fase 3
        return Task.CompletedTask;
    }
}

/// <summary>
/// Implementação inicial de monitoramento de conectividade para MAUI (Fase 3).
/// Na Fase 4 será conectada aos eventos reativos de Connectivity.Current.
/// </summary>
public class MobileConnectivityStub : IConnectivityService
{
    public bool IsOnline => true;

    public event Action<bool>? ConnectivityChanged
    {
        add { }
        remove { }
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// Implementação inicial em memória da fila de operações offline para MAUI (Fase 3).
/// Na Fase 4 será persistida em banco SQLite local (criacerto_offline.db3).
/// </summary>
public class MobileOfflineSyncStorageStub : IOfflineSyncStorage
{
    private readonly List<SyncOperation> _operations = new();

    public Task InitializeAsync() => Task.CompletedTask;

    public Task<IReadOnlyList<SyncOperation>> GetPendingOperationsAsync()
    {
        return Task.FromResult<IReadOnlyList<SyncOperation>>(_operations.AsReadOnly());
    }

    public Task SavePendingOperationAsync(SyncOperation operation)
    {
        _operations.RemoveAll(o => o.Id == operation.Id);
        _operations.Add(operation);
        return Task.CompletedTask;
    }

    public Task RemovePendingOperationAsync(Guid operationId)
    {
        _operations.RemoveAll(o => o.Id == operationId);
        return Task.CompletedTask;
    }

    public Task ClearPendingOperationsAsync()
    {
        _operations.Clear();
        return Task.CompletedTask;
    }
}
