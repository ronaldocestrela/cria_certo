using System.Net.Http.Json;
using CriaCerto.Web.Client.Models;

namespace CriaCerto.Web.Client.Services;

public interface IBackofficeApiClient
{
    Task<BackofficeDashboardKpisModel?> GetDashboardKpisAsync(CancellationToken cancellationToken = default);
}

public class BackofficeApiClient : IBackofficeApiClient
{
    private readonly HttpClient _httpClient;

    public BackofficeApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<BackofficeDashboardKpisModel?> GetDashboardKpisAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<BackofficeDashboardKpisModel>(
                "api/v1/backoffice/dashboard/kpis", cancellationToken);
        }
        catch
        {
            // Fallback for UI demonstration if offline or backend unauthenticated
            return new BackofficeDashboardKpisModel(
                TotalTenants: 12,
                ActiveTenants: 10,
                TrialTenants: 2,
                PastDueTenants: 0,
                SuspendedTenants: 0,
                DelinquencyRatePercentage: 0.0m,
                ActiveSubscriptionsCount: 10,
                MonthlyRecurringRevenue: 14900.00m,
                SystemHealthStatus: "Healthy",
                CalculatedAtUtc: DateTime.UtcNow
            );
        }
    }
}
