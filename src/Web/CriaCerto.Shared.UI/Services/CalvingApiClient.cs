using System.Net.Http.Headers;
using System.Net.Http.Json;
using CriaCerto.Modules.Calving.Application.Contracts;
using CriaCerto.Shared.UI.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace CriaCerto.Shared.UI.Services;

public sealed class CalvingApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ITokenStorageService _tokenStorage;

    [ActivatorUtilitiesConstructor]
    public CalvingApiClient(HttpClient httpClient, ITokenStorageService tokenStorage)
    {
        _httpClient = httpClient;
        _tokenStorage = tokenStorage;
    }

    internal CalvingApiClient(HttpClient httpClient, IJSRuntime jsRuntime)
        : this(httpClient, new JSRuntimeTokenStorageAdapter(jsRuntime))
    {
    }

    public async Task<List<CalvingRecordListItemDto>> GetCalvingRecordsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        try
        {
            var records = await _httpClient.GetFromJsonAsync<List<CalvingRecordListItemDto>>($"api/calving/records?tenantId={tenantId}", cancellationToken);
            return records ?? new List<CalvingRecordListItemDto>();
        }
        catch
        {
            return new List<CalvingRecordListItemDto>();
        }
    }

    public async Task<CalvingDto?> RegisterCalvingAsync(RegisterCalvingCommand command, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        var response = await _httpClient.PostAsJsonAsync("api/calving/calvings", command, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<CalvingDto>(cancellationToken: cancellationToken);
    }

    public async Task<WeaningDto?> RegisterWeaningAsync(RegisterWeaningCommand command, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        var response = await _httpClient.PostAsJsonAsync("api/calving/weanings", command, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<WeaningDto>(cancellationToken: cancellationToken);
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
