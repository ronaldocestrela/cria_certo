using System.Net.Http.Headers;
using System.Net.Http.Json;
using CriaCerto.Shared.UI.Abstractions;
using CriaCerto.Shared.UI.Models;
using Microsoft.JSInterop;

namespace CriaCerto.Shared.UI.Services;

public sealed class GrowthApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ITokenStorageService _tokenStorage;

    public GrowthApiClient(HttpClient httpClient, ITokenStorageService tokenStorage)
    {
        _httpClient = httpClient;
        _tokenStorage = tokenStorage;
    }

    public GrowthApiClient(HttpClient httpClient, IJSRuntime jsRuntime)
        : this(httpClient, new JSRuntimeTokenStorageAdapter(jsRuntime))
    {
    }

    public async Task<List<PaddockStockingRateDto>> GetPaddocksAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        var response = await _httpClient.GetFromJsonAsync<List<PaddockStockingRateDto>>($"api/growth/paddocks?tenantId={tenantId}", cancellationToken);
        return response ?? new List<PaddockStockingRateDto>();
    }

    public async Task<PaddockDto?> CreatePaddockAsync(CreatePaddockCommand command, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        var response = await _httpClient.PostAsJsonAsync("api/growth/paddocks", command, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<PaddockDto>(cancellationToken);
        }
        return null;
    }

    public async Task<List<LotDto>> GetLotsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        var response = await _httpClient.GetFromJsonAsync<List<LotDto>>($"api/growth/lots?tenantId={tenantId}", cancellationToken);
        return response ?? new List<LotDto>();
    }

    public async Task<LotDto?> CreateLotAsync(CreateLotCommand command, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        var response = await _httpClient.PostAsJsonAsync("api/growth/lots", command, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<LotDto>(cancellationToken);
        }
        return null;
    }

    public async Task<LotMovementDto?> MoveLotAsync(MoveLotToPaddockCommand command, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        var response = await _httpClient.PostAsJsonAsync("api/growth/lots/move", command, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<LotMovementDto>(cancellationToken);
        }
        return null;
    }

    public async Task<LotDto?> CloseLotAsync(Guid lotId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        var response = await _httpClient.PostAsync($"api/growth/lots/{lotId}/close?tenantId={tenantId}", null, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<LotDto>(cancellationToken);
        }
        return null;
    }

    public async Task<ImportWeighingFileResultDto?> ImportWeighingFileAsync(
        byte[] fileBytes,
        string fileName,
        int scaleModel,
        Guid tenantId,
        Guid? lotId = null,
        DateTime? defaultDate = null,
        decimal defaultYield = 50.0m,
        CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(fileContent, "file", fileName);

        string url = $"api/growth/weighings/import?scaleModel={scaleModel}&tenantId={tenantId}&defaultCarcassYield={defaultYield}";
        if (lotId.HasValue) url += $"&lotId={lotId.Value}";
        if (defaultDate.HasValue) url += $"&defaultWeighingDate={defaultDate.Value:yyyy-MM-ddTHH:mm:ss}";

        var response = await _httpClient.PostAsync(url, content, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<ImportWeighingFileResultDto>(cancellationToken);
        }
        return null;
    }

    public async Task<SlaughterEligibilityDto?> ValidateSlaughterEligibilityAsync(Guid animalId, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        try
        {
            return await _httpClient.GetFromJsonAsync<SlaughterEligibilityDto>($"api/sanitary/slaughter-validation/{animalId}", cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    public async Task<HttpResponseMessage> DispatchAnimalAsync(DispatchAnimalCommand command, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        return await _httpClient.PostAsJsonAsync("api/growth/dispatch/animal", command, cancellationToken);
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
