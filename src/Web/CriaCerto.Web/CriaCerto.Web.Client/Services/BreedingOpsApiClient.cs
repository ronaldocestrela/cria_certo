using System.Net.Http.Headers;
using System.Net.Http.Json;
using CriaCerto.Web.Client.Models;
using Microsoft.JSInterop;

namespace CriaCerto.Web.Client.Services;

public sealed class BreedingOpsApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IJSRuntime _jsRuntime;

    public BreedingOpsApiClient(HttpClient httpClient, IJSRuntime jsRuntime)
    {
        _httpClient = httpClient;
        _jsRuntime = jsRuntime;
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
        var response = await _httpClient.PostAsJsonAsync("api/breeding/diagnoses", request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> EnqueueDiagnosisAsync(RegisterPregnancyDiagnosisRequest request)
    {
        return true;
    }

    public async Task<RegisterBreedingBatchResponse?> RegisterBreedingBatchAsync(RegisterBreedingBatchRequest request, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        return new RegisterBreedingBatchResponse(new List<BreedingEventDto>(), request.Lines.Count);
    }

    public async Task<bool> EnqueueBreedingBatchAsync(RegisterBreedingBatchRequest request)
    {
        return true;
    }

    public async Task<int> SyncPendingOpsAsync(Func<string, string, Task<bool>> handler)
    {
        return 0;
    }

    public async Task<bool> IsOnlineAsync()
    {
        return true;
    }

    public async Task<int> GetPendingOpsCountAsync()
    {
        return 0;
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
