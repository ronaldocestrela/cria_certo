using System.Net.Http.Headers;
using System.Net.Http.Json;
using CriaCerto.Web.Client.Models;
using Microsoft.JSInterop;

namespace CriaCerto.Web.Client.Services;

public sealed class BreedingOpsApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IJSRuntime _jsRuntime;
    private readonly IOfflineSyncService _offlineSyncService;

    public BreedingOpsApiClient(
        HttpClient httpClient,
        IJSRuntime jsRuntime,
        IOfflineSyncService offlineSyncService)
    {
        _httpClient = httpClient;
        _jsRuntime = jsRuntime;
        _offlineSyncService = offlineSyncService;
    }

    public async Task<List<IatfProtocolDto>> GetIatfProtocolsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        try
        {
            var protocols = await _httpClient.GetFromJsonAsync<List<IatfProtocolDto>>($"api/breeding/iatf-protocols?tenantId={tenantId}", cancellationToken);
            return protocols ?? new List<IatfProtocolDto>();
        }
        catch
        {
            return new List<IatfProtocolDto>();
        }
    }

    public async Task<IatfProtocolDto?> RegisterIatfProtocolAsync(string name, DateTime startDate, DateTime inseminationDate, Guid semenBatchId, List<Guid> cowIds, Guid tenantId, Guid? bullId = null, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        var request = new { Name = name, StartDate = startDate, InseminationDate = inseminationDate, SemenBatchId = semenBatchId, CowIds = cowIds, TenantId = tenantId, BullId = bullId };
        var response = await _httpClient.PostAsJsonAsync("api/breeding/iatf-protocols", request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<IatfProtocolDto>(cancellationToken: cancellationToken);
    }

    public async Task<PregnancyCheckQueueResponse?> ListPregnancyChecksAsync(string? search, int page = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        return new PregnancyCheckQueueResponse(new List<PregnancyCheckTaskDto>(), 0, 0, 0, page, pageSize);
    }

    public async Task<bool?> RegisterDiagnosisAsync(RegisterPregnancyDiagnosisRequest request, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/breeding/diagnoses", request, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            _offlineSyncService.SetNetworkStatus(false);
            var enqueued = await EnqueueDiagnosisAsync(request);
            return enqueued ? true : null;
        }
    }

    public async Task<bool> EnqueueDiagnosisAsync(RegisterPregnancyDiagnosisRequest request)
    {
        await _offlineSyncService.InitializeAsync();
        var result = await _offlineSyncService.EnqueueOperationAsync("Breeding", "RegisterDiagnosis", request);
        return result.IsSuccess;
    }

    public async Task<RegisterBreedingBatchResponse?> RegisterBreedingBatchAsync(RegisterBreedingBatchRequest request, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/v1/sync/Breeding/RegisterBreedingBatch", request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return new RegisterBreedingBatchResponse(new List<BreedingEventDto>(), request.Lines.Count);
        }
        catch (HttpRequestException)
        {
            _offlineSyncService.SetNetworkStatus(false);
            var enqueued = await EnqueueBreedingBatchAsync(request);
            return enqueued
                ? new RegisterBreedingBatchResponse(new List<BreedingEventDto>(), request.Lines.Count)
                : null;
        }
    }

    public async Task<bool> EnqueueBreedingBatchAsync(RegisterBreedingBatchRequest request)
    {
        await _offlineSyncService.InitializeAsync();
        var result = await _offlineSyncService.EnqueueOperationAsync("Breeding", "RegisterBreedingBatch", request);
        return result.IsSuccess;
    }

    public async Task<int> SyncPendingOpsAsync(Func<string, string, Task<bool>>? handler = null)
    {
        await _offlineSyncService.InitializeAsync();
        if (!_offlineSyncService.IsOnline || _offlineSyncService.PendingCount == 0)
            return 0;

        var beforeCount = _offlineSyncService.PendingCount;
        await _offlineSyncService.ForceSyncAsync();
        var afterCount = _offlineSyncService.PendingCount;
        return Math.Max(0, beforeCount - afterCount);
    }

    public async Task<bool> IsOnlineAsync()
    {
        await _offlineSyncService.InitializeAsync();
        return _offlineSyncService.IsOnline;
    }

    public async Task<int> GetPendingOpsCountAsync()
    {
        await _offlineSyncService.InitializeAsync();
        return _offlineSyncService.PendingCount;
    }

    private async Task AttachTokenAsync()
    {
        try
        {
            var token = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "authToken");
            _httpClient.DefaultRequestHeaders.Authorization = string.IsNullOrWhiteSpace(token)
                ? null
                : new AuthenticationHeaderValue("Bearer", token);
        }
        catch
        {
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }
    }
}
