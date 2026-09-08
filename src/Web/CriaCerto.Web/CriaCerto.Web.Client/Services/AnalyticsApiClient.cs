using System.Net.Http.Headers;
using System.Net.Http.Json;
using CriaCerto.Modules.Analytics.Application.Contracts;
using Microsoft.JSInterop;

namespace CriaCerto.Web.Client.Services;

public sealed class AnalyticsApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IJSRuntime _jsRuntime;

    public AnalyticsApiClient(HttpClient httpClient, IJSRuntime jsRuntime)
    {
        _httpClient = httpClient;
        _jsRuntime = jsRuntime;
    }

    public async Task<ExecutiveDashboardDto?> GetExecutiveDashboardAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        var url = tenantId.HasValue && tenantId.Value != Guid.Empty
            ? $"api/analytics/dashboard?tenantId={tenantId.Value}"
            : "api/analytics/dashboard";

        var response = await _httpClient.GetAsync(url, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<ExecutiveDashboardDto>(cancellationToken);
        }

        return null;
    }

    public async Task<ExecutiveScorecardDto?> GetExecutiveScorecardAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        var url = tenantId.HasValue && tenantId.Value != Guid.Empty
            ? $"api/analytics/executive-scorecard?tenantId={tenantId.Value}"
            : "api/analytics/executive-scorecard";

        var response = await _httpClient.GetAsync(url, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<ExecutiveScorecardDto>(cancellationToken);
        }

        return null;
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
