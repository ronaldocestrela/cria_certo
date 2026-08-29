using System.Net.Http.Headers;
using System.Net.Http.Json;
using CriaCerto.Web.Client.Models;
using Microsoft.JSInterop;

namespace CriaCerto.Web.Client.Services;

public sealed class PlantelApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IJSRuntime _jsRuntime;

    public PlantelApiClient(HttpClient httpClient, IJSRuntime jsRuntime)
    {
        _httpClient = httpClient;
        _jsRuntime = jsRuntime;
    }

    public async Task<CattleListResponse<CowSummaryDto>?> ListCowsAsync(string? search, ReproductiveStatus? status, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query.Add($"search={Uri.EscapeDataString(search)}");
        }

        if (status.HasValue)
        {
            query.Add($"status={status.Value}");
        }

        var url = "api/breeding/cows" + (query.Count > 0 ? "?" + string.Join('&', query) : string.Empty);
        return await _httpClient.GetFromJsonAsync<CattleListResponse<CowSummaryDto>>(url, cancellationToken);
    }

    public async Task<List<BullSummaryDto>> ListBullsAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        try
        {
            var url = tenantId.HasValue && tenantId.Value != Guid.Empty
                ? $"api/breeding/bulls?tenantId={tenantId.Value}"
                : "api/breeding/bulls";
            var result = await _httpClient.GetFromJsonAsync<List<BullSummaryDto>>(url, cancellationToken);
            return result ?? new List<BullSummaryDto>();
        }
        catch
        {
            return new List<BullSummaryDto>();
        }
    }

    public async Task<CowDetailDto?> GetCowAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        return await _httpClient.GetFromJsonAsync<CowDetailDto>($"api/breeding/cows/{id}", cancellationToken);
    }

    public async Task<HttpResponseMessage> CreateCowAsync(CreateAnimalRequest request, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        return await _httpClient.PostAsJsonAsync("api/breeding/cows", request, cancellationToken);
    }

    public async Task<HttpResponseMessage> UpdateCowAsync(Guid id, UpdateAnimalRequest request, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        return await _httpClient.PutAsJsonAsync($"api/breeding/cows/{id}", request, cancellationToken);
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
