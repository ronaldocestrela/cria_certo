using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Web.Client.Models;
using Microsoft.JSInterop;

namespace CriaCerto.Web.Client.Services;

public sealed class OfflineSyncService : IOfflineSyncService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly HttpClient _httpClient;
    private readonly List<SyncOperation> _pendingOperations = new();
    private readonly List<SyncConflictItem> _activeConflicts = new();
    private DotNetObjectReference<OfflineSyncService>? _dotNetRef;
    private bool _initialized;

    public bool IsOnline { get; private set; } = true;
    public bool IsSyncing { get; private set; }
    public int PendingCount => _pendingOperations.Count;
    public IReadOnlyList<SyncOperation> PendingOperations => _pendingOperations.AsReadOnly();
    public IReadOnlyList<SyncConflictItem> ActiveConflicts => _activeConflicts.AsReadOnly();

    public event Action? OnStateChanged;

    public OfflineSyncService(IJSRuntime jsRuntime, HttpClient httpClient)
    {
        _jsRuntime = jsRuntime;
        _httpClient = httpClient;
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        try
        {
            _dotNetRef = DotNetObjectReference.Create(this);
            IsOnline = await _jsRuntime.InvokeAsync<bool>("criaCertoOfflineSync.init", _dotNetRef);
            await RefreshPendingOperationsFromStoreAsync();
            _initialized = true;
            NotifyStateChanged();
        }
        catch
        {
            // Fallback para ambiente de testes unitários sem ambiente JS completo
            IsOnline = true;
            _initialized = true;
        }
    }

    [JSInvokable]
    public void OnNetworkStatusChanged(bool isOnline)
    {
        SetNetworkStatus(isOnline);
    }

    public void SetNetworkStatus(bool isOnline)
    {
        IsOnline = isOnline;
        NotifyStateChanged();

        if (isOnline && PendingCount > 0)
        {
            _ = ForceSyncAsync();
        }
    }

    public async Task<Result> EnqueueOperationAsync<T>(string moduleName, string actionType, T payload)
    {
        var payloadJson = payload is string rawJson && IsRawJsonObjectOrArray(rawJson)
            ? rawJson.Trim()
            : JsonSerializer.Serialize(payload);

        var op = new SyncOperation
        {
            Id = Guid.NewGuid(),
            ModuleName = moduleName,
            ActionType = actionType,
            PayloadJson = payloadJson,
            CreatedAtUtc = DateTime.UtcNow,
            Status = SyncOperationStatus.Pending
        };

        _pendingOperations.Add(op);
        NotifyStateChanged();

        try
        {
            await _jsRuntime.InvokeVoidAsync("criaCertoOfflineSync.enqueueOperation", op);
        }
        catch
        {
            // Ignorado em ambientes sem suporte JS no-op
        }

        return Result.Success();
    }

    public async Task<Result> ForceSyncAsync()
    {
        if (!IsOnline || IsSyncing || _pendingOperations.Count == 0)
        {
            return Result.Success();
        }

        IsSyncing = true;
        NotifyStateChanged();

        var operationsToProcess = _pendingOperations.ToList();

        try
        {
            await AttachTokenAsync();
            foreach (var op in operationsToProcess)
            {
                op.Status = SyncOperationStatus.Syncing;
                NotifyStateChanged();

                try
                {
                    using var content = new StringContent(op.PayloadJson, Encoding.UTF8, "application/json");
                    var response = await _httpClient.PostAsync($"api/v1/sync/{op.ModuleName}/{op.ActionType}", content);

                    if (response.IsSuccessStatusCode)
                    {
                        op.Status = SyncOperationStatus.Success;
                        _pendingOperations.Remove(op);
                        await RemoveFromIndexedDbAsync(op.Id);
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                    {
                        op.Status = SyncOperationStatus.Conflict;
                        var serverContent = await response.Content.ReadAsStringAsync();

                        if (!_activeConflicts.Any(c => c.OperationId == op.Id))
                        {
                            var conflict = new SyncConflictItem
                            {
                                OperationId = op.Id,
                                EntityName = op.ModuleName,
                                EntityIdentifier = op.ActionType,
                                LocalPayloadJson = op.PayloadJson,
                                ServerPayloadJson = serverContent,
                                ConflictReason = "Divergência de dados entre a operação de campo e o servidor central."
                            };

                            _activeConflicts.Add(conflict);
                        }
                    }
                    else
                    {
                        op.Status = SyncOperationStatus.Failed;
                        op.ErrorMessage = $"Erro HTTP {(int)response.StatusCode}: {response.ReasonPhrase}";
                    }
                }
                catch (Exception ex)
                {
                    op.Status = SyncOperationStatus.Failed;
                    op.ErrorMessage = ex.Message;
                }
            }

            return _activeConflicts.Count > 0
                ? Result.Failure(Error.Conflict("Sync.ConflictDetected", "Ocorreram conflitos durante a sincronização que exigem mediação."))
                : Result.Success();
        }
        finally
        {
            IsSyncing = false;
            NotifyStateChanged();
        }
    }

    public async Task<Result> ResolveConflictAsync(Guid operationId, ConflictResolutionOption resolution)
    {
        var conflict = _activeConflicts.FirstOrDefault(c => c.OperationId == operationId);
        var op = _pendingOperations.FirstOrDefault(o => o.Id == operationId);

        if (conflict is null || op is null)
        {
            return Result.Failure(Error.NotFound("Sync.ConflictNotFound", "Conflito não encontrado."));
        }

        if (resolution == ConflictResolutionOption.UseLocal)
        {
            try
            {
                await AttachTokenAsync();
                // Força a substituição no servidor
                using var content = new StringContent(op.PayloadJson, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"api/v1/sync/{op.ModuleName}/{op.ActionType}?force=true", content);
                if (response.IsSuccessStatusCode)
                {
                    _pendingOperations.Remove(op);
                    _activeConflicts.Remove(conflict);
                    await RemoveFromIndexedDbAsync(op.Id);
                }
                else
                {
                    return Result.Failure(Error.Failure("Sync.ResolutionFailed", "Falha ao aplicar a versão local no servidor."));
                }
            }
            catch (Exception ex)
            {
                op.Status = SyncOperationStatus.Failed;
                op.ErrorMessage = ex.Message;
                NotifyStateChanged();
                return Result.Failure(Error.Failure("Sync.ResolutionFailed", $"Falha de conexão ao aplicar a versão local: {ex.Message}"));
            }
        }
        else if (resolution == ConflictResolutionOption.UseServer)
        {
            // Descarta a operação local
            _pendingOperations.Remove(op);
            _activeConflicts.Remove(conflict);
            await RemoveFromIndexedDbAsync(op.Id);
        }

        NotifyStateChanged();
        return Result.Success();
    }

    public async Task<Result> ClearPendingOperationsAsync()
    {
        _pendingOperations.Clear();
        _activeConflicts.Clear();

        try
        {
            await _jsRuntime.InvokeVoidAsync("criaCertoOfflineSync.clearQueue");
        }
        catch
        {
            // Ignorado em ambientes de teste sem JS
        }

        NotifyStateChanged();
        return Result.Success();
    }

    private async Task RefreshPendingOperationsFromStoreAsync()
    {
        try
        {
            var storedOps = await _jsRuntime.InvokeAsync<List<SyncOperation>>("criaCertoOfflineSync.getPendingOperations");
            if (storedOps != null && storedOps.Count > 0)
            {
                _pendingOperations.Clear();
                _pendingOperations.AddRange(storedOps);
            }
        }
        catch
        {
            // Ignorado em ambientes de teste
        }
    }

    private async Task RemoveFromIndexedDbAsync(Guid id)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("criaCertoOfflineSync.removeOperation", id);
        }
        catch
        {
            // Ignorado em ambientes de teste
        }
    }

    private void NotifyStateChanged() => OnStateChanged?.Invoke();

    private static bool IsRawJsonObjectOrArray(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.AsSpan().Trim();
        return (trimmed.StartsWith("{") && trimmed.EndsWith("}")) ||
               (trimmed.StartsWith("[") && trimmed.EndsWith("]"));
    }

    private async Task AttachTokenAsync()
    {
        try
        {
            var token = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "authToken");
            _httpClient.DefaultRequestHeaders.Authorization = string.IsNullOrWhiteSpace(token)
                ? null
                : new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }
        catch
        {
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }
    }
}
