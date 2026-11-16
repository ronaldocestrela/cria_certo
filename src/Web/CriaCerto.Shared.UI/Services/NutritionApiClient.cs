using System.Net.Http.Headers;
using System.Net.Http.Json;
using CriaCerto.Modules.Nutrition.Application.Contracts;
using CriaCerto.Modules.Nutrition.Application.Features.AnalyticsFeatures;
using CriaCerto.Modules.Nutrition.Application.Features.FeedingFeatures;
using CriaCerto.Modules.Nutrition.Application.Features.RationFeatures;
using CriaCerto.Modules.Nutrition.Application.Features.SiloStockFeatures;
using CriaCerto.Shared.UI.Abstractions;
using Microsoft.JSInterop;

namespace CriaCerto.Shared.UI.Services;

public sealed class NutritionApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ITokenStorageService _tokenStorage;

    public NutritionApiClient(HttpClient httpClient, ITokenStorageService tokenStorage)
    {
        _httpClient = httpClient;
        _tokenStorage = tokenStorage;
    }

    public NutritionApiClient(HttpClient httpClient, IJSRuntime jsRuntime)
        : this(httpClient, new JSRuntimeTokenStorageAdapter(jsRuntime))
    {
    }

    public async Task<List<SiloStockDto>> GetSilosAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        var response = await _httpClient.GetFromJsonAsync<List<SiloStockDto>>($"api/nutrition/silos?tenantId={tenantId}", cancellationToken);
        return response ?? new List<SiloStockDto>();
    }

    public async Task<SiloStockDto?> CreateSiloAsync(CreateSiloStockCommand command, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        var response = await _httpClient.PostAsJsonAsync("api/nutrition/silos", command, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<SiloStockDto>(cancellationToken);
        }
        return null;
    }

    public async Task<SiloStockDto?> RestockSiloAsync(RestockSiloCommand command, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        var response = await _httpClient.PostAsJsonAsync("api/nutrition/silos/restock", command, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<SiloStockDto>(cancellationToken);
        }
        return null;
    }

    public async Task<List<FeedRationDto>> GetRationsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        var response = await _httpClient.GetFromJsonAsync<List<FeedRationDto>>($"api/nutrition/rations?tenantId={tenantId}", cancellationToken);
        return response ?? new List<FeedRationDto>();
    }

    public async Task<FeedRationDto?> CreateRationAsync(CreateFeedRationCommand command, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        var response = await _httpClient.PostAsJsonAsync("api/nutrition/rations", command, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<FeedRationDto>(cancellationToken);
        }
        return null;
    }

    public async Task<PastureSupplementationDto?> RecordSupplementationAsync(RecordSupplementationCommand command, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        var response = await _httpClient.PostAsJsonAsync("api/nutrition/supplementation", command, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<PastureSupplementationDto>(cancellationToken);
        }
        return null;
    }

    public async Task<DailyFeedBatchDto?> RecordTmrBatchAsync(RecordFeedlotTmrCommand command, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        var response = await _httpClient.PostAsJsonAsync("api/nutrition/tmr-batches", command, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<DailyFeedBatchDto>(cancellationToken);
        }
        return null;
    }

    public async Task<FeedlotPerformanceDto?> GetFeedlotPerformanceAsync(Guid tenantId, Guid lotId, decimal totalWeightGainKg, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        var response = await _httpClient.GetFromJsonAsync<FeedlotPerformanceDto>($"api/nutrition/analytics/feed-conversion?tenantId={tenantId}&lotId={lotId}&totalWeightGainKg={totalWeightGainKg}", cancellationToken);
        return response;
    }

    public async Task<CostPerArrobaDto?> GetCostPerArrobaAsync(Guid tenantId, Guid lotId, decimal totalWeightGainKg, decimal? carcassYieldPercentage, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        var url = $"api/nutrition/analytics/cost-per-arroba?tenantId={tenantId}&lotId={lotId}&totalWeightGainKg={totalWeightGainKg}";
        if (carcassYieldPercentage.HasValue)
        {
            url += $"&carcassYieldPercentage={carcassYieldPercentage.Value}";
        }
        var response = await _httpClient.GetFromJsonAsync<CostPerArrobaDto>(url, cancellationToken);
        return response;
    }

    private async Task AttachTokenAsync()
    {
        try
        {
            var token = await _tokenStorage.GetTokenAsync();
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
