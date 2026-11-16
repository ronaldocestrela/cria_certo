using CriaCerto.Shared.UI.Abstractions;
using CriaCerto.Shared.UI.Models;
using Microsoft.JSInterop;

namespace CriaCerto.Web.Client.Services.Browser;

/// <summary>
/// Implementação browser de IOfflineSyncStorage utilizando IndexedDB via criaCertoOfflineSync.
/// </summary>
public class BrowserOfflineSyncStorage : IOfflineSyncStorage
{
    private readonly IJSRuntime _jsRuntime;

    public BrowserOfflineSyncStorage(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public Task InitializeAsync()
    {
        // A inicialização da base IndexedDB é garantida pela chamada a openDatabase em offlineSync.js
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<SyncOperation>> GetPendingOperationsAsync()
    {
        if (!OperatingSystem.IsBrowser())
        {
            return Array.Empty<SyncOperation>();
        }

        try
        {
            var storedOps = await _jsRuntime.InvokeAsync<List<SyncOperation>>("criaCertoOfflineSync.getPendingOperations");
            return storedOps ?? (IReadOnlyList<SyncOperation>)Array.Empty<SyncOperation>();
        }
        catch
        {
            return Array.Empty<SyncOperation>();
        }
    }

    public async Task SavePendingOperationAsync(SyncOperation operation)
    {
        if (!OperatingSystem.IsBrowser())
        {
            return;
        }

        try
        {
            await _jsRuntime.InvokeVoidAsync("criaCertoOfflineSync.enqueueOperation", operation);
        }
        catch
        {
            // Tratamento silencioso em ambientes sem suporte a IndexedDB
        }
    }

    public async Task RemovePendingOperationAsync(Guid operationId)
    {
        if (!OperatingSystem.IsBrowser())
        {
            return;
        }

        try
        {
            await _jsRuntime.InvokeVoidAsync("criaCertoOfflineSync.removeOperation", operationId);
        }
        catch
        {
            // Tratamento silencioso
        }
    }

    public async Task ClearPendingOperationsAsync()
    {
        if (!OperatingSystem.IsBrowser())
        {
            return;
        }

        try
        {
            await _jsRuntime.InvokeVoidAsync("criaCertoOfflineSync.clearQueue");
        }
        catch
        {
            // Tratamento silencioso
        }
    }
}

