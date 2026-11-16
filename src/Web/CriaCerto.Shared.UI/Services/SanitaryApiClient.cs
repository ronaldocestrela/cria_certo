using System.Net.Http.Headers;
using System.Net.Http.Json;
using CriaCerto.Modules.Sanitary.Application.Contracts;
using CriaCerto.Shared.UI.Abstractions;
using Microsoft.JSInterop;

namespace CriaCerto.Shared.UI.Services;

public sealed class SanitaryApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ITokenStorageService _tokenStorage;

    public SanitaryApiClient(HttpClient httpClient, ITokenStorageService tokenStorage)
    {
        _httpClient = httpClient;
        _tokenStorage = tokenStorage;
    }

    public SanitaryApiClient(HttpClient httpClient, IJSRuntime jsRuntime)
        : this(httpClient, new JSRuntimeTokenStorageAdapter(jsRuntime))
    {
    }

    public async Task<List<VaccinationCampaignDto>> GetActiveCampaignsAsync(CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        try
        {
            var response = await _httpClient.GetFromJsonAsync<List<VaccinationCampaignDto>>("api/sanitary/campaigns", cancellationToken);
            return response ?? new List<VaccinationCampaignDto>();
        }
        catch
        {
            return new List<VaccinationCampaignDto>();
        }
    }

    public async Task<VaccinationCampaignDto?> CreateCampaignAsync(CreateVaccinationCampaignCommand command, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        var response = await _httpClient.PostAsJsonAsync("api/sanitary/campaigns", command, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<VaccinationCampaignDto>(cancellationToken: cancellationToken);
    }

    public async Task<List<TreatmentRecordDto>> GetTreatmentsAsync(CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        try
        {
            var response = await _httpClient.GetFromJsonAsync<List<TreatmentRecordDto>>("api/sanitary/treatments", cancellationToken);
            return response ?? new List<TreatmentRecordDto>();
        }
        catch
        {
            return new List<TreatmentRecordDto>();
        }
    }

    public async Task<TreatmentRecordDto?> ApplyTreatmentAsync(ApplyTreatmentCommand command, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        var response = await _httpClient.PostAsJsonAsync("api/sanitary/treatments", command, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<TreatmentRecordDto>(cancellationToken: cancellationToken);
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
