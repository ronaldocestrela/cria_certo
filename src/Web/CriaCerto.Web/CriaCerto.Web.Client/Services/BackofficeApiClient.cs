using System.Net.Http.Headers;
using System.Net.Http.Json;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Features.AdminUsers.Dtos;
using CriaCerto.Modules.Backoffice.Application.Features.AdminUsers.Queries;
using CriaCerto.Web.Client.Models;
using Microsoft.JSInterop;

namespace CriaCerto.Web.Client.Services;

public interface IBackofficeApiClient
{
    Task<BackofficeLoginResponse> LoginAsync(string email, string password, string? mfaCode = null, CancellationToken cancellationToken = default);
    Task<BackofficeDashboardKpisModel?> GetDashboardKpisAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<AdminUserSummaryDto>?> GetAdminUsersAsync(string? searchTerm = null, bool? isActive = null, string? roleName = null, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<AdminUserDetailDto?> GetAdminUserByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> CreateAdminUserAsync(string name, string email, string password, List<Guid> roleIds, CancellationToken cancellationToken = default);
    Task<bool> UpdateAdminUserAsync(Guid id, string name, string email, List<Guid> roleIds, CancellationToken cancellationToken = default);
    Task<bool> ToggleAdminUserStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);
    Task<bool> ResetAdminUserPasswordAsync(Guid id, string newPassword, CancellationToken cancellationToken = default);
    Task<MfaSetupResultDto?> SetupMfaAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> EnableMfaAsync(Guid id, string secretKey, string verificationCode, List<string> recoveryCodes, CancellationToken cancellationToken = default);
    Task<bool> DisableMfaAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> RevokeSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<bool> RevokeAllUserSessionsAsync(Guid userId, CancellationToken cancellationToken = default);
}

public class BackofficeApiClient : IBackofficeApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IJSRuntime _jsRuntime;

    public BackofficeApiClient(HttpClient httpClient, IJSRuntime jsRuntime)
    {
        _httpClient = httpClient;
        _jsRuntime = jsRuntime;
    }

    public async Task<BackofficeLoginResponse> LoginAsync(string email, string password, string? mfaCode = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "api/v1/backoffice/auth/login",
            new { Email = email, Password = password, MfaCode = mfaCode },
            cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var authResult = await response.Content.ReadFromJsonAsync<AdminAuthResultDto>(cancellationToken: cancellationToken);
            return new BackofficeLoginResponse { AuthResult = authResult };
        }

        Error? error = null;
        try
        {
            error = await response.Content.ReadFromJsonAsync<Error>(cancellationToken: cancellationToken);
        }
        catch
        {
            // Ignore JSON parse failures and fall back to generic message.
        }

        if (error?.Code == "Backoffice.MfaRequired")
        {
            return new BackofficeLoginResponse
            {
                MfaRequired = true,
                ErrorCode = error.Code,
                ErrorMessage = error.Message
            };
        }

        return new BackofficeLoginResponse
        {
            ErrorCode = error?.Code,
            ErrorMessage = error?.Message ?? "Credenciais inválidas ou e-mail/senha incorretos."
        };
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
    public async Task<BackofficeDashboardKpisModel?> GetDashboardKpisAsync(CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        return await _httpClient.GetFromJsonAsync<BackofficeDashboardKpisModel>(
            "api/v1/backoffice/dashboard/kpis", cancellationToken);
    }

    public async Task<PagedResult<AdminUserSummaryDto>?> GetAdminUsersAsync(string? searchTerm = null, bool? isActive = null, string? roleName = null, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        var url = $"api/v1/backoffice/users?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(searchTerm)) url += $"&searchTerm={Uri.EscapeDataString(searchTerm)}";
        if (isActive.HasValue) url += $"&isActive={isActive.Value}";
        if (!string.IsNullOrWhiteSpace(roleName)) url += $"&roleName={Uri.EscapeDataString(roleName)}";

        return await _httpClient.GetFromJsonAsync<PagedResult<AdminUserSummaryDto>>(url, cancellationToken);
    }

    public async Task<AdminUserDetailDto?> GetAdminUserByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        return await _httpClient.GetFromJsonAsync<AdminUserDetailDto>($"api/v1/backoffice/users/{id}", cancellationToken);
    }

    public async Task<bool> CreateAdminUserAsync(string name, string email, string password, List<Guid> roleIds, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        var response = await _httpClient.PostAsJsonAsync("api/v1/backoffice/users", new { name, email, rawPassword = password, roleIds }, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateAdminUserAsync(Guid id, string name, string email, List<Guid> roleIds, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        var response = await _httpClient.PutAsJsonAsync($"api/v1/backoffice/users/{id}", new { name, email, roleIds }, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ToggleAdminUserStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        var response = await _httpClient.PatchAsJsonAsync($"api/v1/backoffice/users/{id}/status", new { isActive }, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ResetAdminUserPasswordAsync(Guid id, string newPassword, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        var response = await _httpClient.PostAsJsonAsync($"api/v1/backoffice/users/{id}/reset-password", new { newRawPassword = newPassword }, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<MfaSetupResultDto?> SetupMfaAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        var response = await _httpClient.PostAsJsonAsync($"api/v1/backoffice/users/{id}/mfa/setup", new { }, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<MfaSetupResultDto>(cancellationToken: cancellationToken);
    }

    public async Task<bool> EnableMfaAsync(Guid id, string secretKey, string verificationCode, List<string> recoveryCodes, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        var response = await _httpClient.PostAsJsonAsync($"api/v1/backoffice/users/{id}/mfa/enable", new { secretKey, verificationCode, recoveryCodes }, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DisableMfaAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        var response = await _httpClient.PostAsJsonAsync($"api/v1/backoffice/users/{id}/mfa/disable", new { }, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> RevokeSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        var response = await _httpClient.DeleteAsync($"api/v1/backoffice/sessions/{sessionId}", cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> RevokeAllUserSessionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        var response = await _httpClient.DeleteAsync($"api/v1/backoffice/users/{userId}/sessions", cancellationToken);
        return response.IsSuccessStatusCode;
    }
}
