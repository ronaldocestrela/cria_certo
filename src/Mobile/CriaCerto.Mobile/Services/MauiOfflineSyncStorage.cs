using CriaCerto.Shared.UI.Abstractions;
using CriaCerto.Shared.UI.Models;
using SQLite;

namespace CriaCerto.Mobile.Services;

/// <summary>
/// Provedor durável de sincronização offline baseado em SQLite para .NET MAUI.
/// Garante que operações pecuárias realizadas sem sinal no pasto/curral sejam armazenadas
/// de forma atômica no banco local criacerto_offline.db3.
/// </summary>
public class MauiOfflineSyncStorage : IOfflineSyncStorage
{
    private readonly SQLiteAsyncConnection _connection;
    private bool _isInitialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public MauiOfflineSyncStorage(string? databasePath = null)
    {
        var dbPath = databasePath ?? Path.Combine(FileSystem.AppDataDirectory, "criacerto_offline.db3");
        _connection = new SQLiteAsyncConnection(dbPath);
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        await _initLock.WaitAsync();
        try
        {
            if (!_isInitialized)
            {
                await _connection.CreateTableAsync<OfflineSyncOperationEntity>();
                _isInitialized = true;
            }
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<IReadOnlyList<SyncOperation>> GetPendingOperationsAsync()
    {
        await InitializeAsync();
        var entities = await _connection.Table<OfflineSyncOperationEntity>()
            .OrderBy(o => o.CreatedAtUtc)
            .ToListAsync();

        return entities.Select(e => e.ToModel()).ToList();
    }

    public async Task SavePendingOperationAsync(SyncOperation operation)
    {
        await InitializeAsync();
        var entity = OfflineSyncOperationEntity.FromModel(operation);
        await _connection.InsertOrReplaceAsync(entity);
    }

    public async Task RemovePendingOperationAsync(Guid operationId)
    {
        await InitializeAsync();
        await _connection.DeleteAsync<OfflineSyncOperationEntity>(operationId);
    }

    public async Task ClearPendingOperationsAsync()
    {
        await InitializeAsync();
        await _connection.DeleteAllAsync<OfflineSyncOperationEntity>();
    }
}

