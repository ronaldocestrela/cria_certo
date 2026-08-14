using System.Net.Http.Headers;
using System.Net.Http.Json;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Features.AdminUsers.Dtos;
using CriaCerto.Modules.Backoffice.Application.Features.AdminUsers.Queries;
using CriaCerto.Modules.Backoffice.Application.Features.Tenants.Dtos;
using CriaCerto.Modules.Backoffice.Application.Features.Plans.Dtos;
using CriaCerto.Modules.Backoffice.Application.Features.Plans.Commands;
using CriaCerto.Modules.Backoffice.Application.Features.Plans.Queries;
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
    Task<PagedTenantAdminResult?> GetTenantsAsync(
        string? searchTerm = null,
        string? status = null,
        string? subscribedPlan = null,
        string? state = null,
        string? ownerSearch = null,
        string? sizeSegment = null,
        string? commercialRegion = null,
        string? productiveProfile = null,
        string? churnRisk = null,
        IReadOnlyCollection<Guid>? tagIds = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<OperationalTagAdminDto>?> GetOperationalTagsAsync(CancellationToken cancellationToken = default);
    Task<OperationalTagAdminDto?> CreateOperationalTagAsync(CreateOperationalTagAdminRequest request, CancellationToken cancellationToken = default);
    Task<TenantAdminDetailDto?> UpdateTenantSegmentationAsync(Guid id, UpdateTenantSegmentationAdminRequest request, CancellationToken cancellationToken = default);
    Task<TenantAdminDetailDto?> ReplaceTenantTagsAsync(Guid id, ReplaceTenantTagsAdminRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<AdminSavedFilterDto>?> GetSavedFiltersAsync(CancellationToken cancellationToken = default);
    Task<AdminSavedFilterDto?> SaveFilterAsync(SaveAdminFilterRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteSavedFilterAsync(Guid filterId, CancellationToken cancellationToken = default);
    Task<ExportTenantsResult?> ExportTenantsCsvAsync(TenantAdminFilterDto filter, CancellationToken cancellationToken = default);
    Task<TenantAdminDetailDto?> GetTenantByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TenantAdminDetailDto?> CreateTenantAsync(CreateTenantAdminRequest request, CancellationToken cancellationToken = default);
    Task<TenantAdminDetailDto?> UpdateTenantAsync(Guid id, UpdateTenantAdminRequest request, CancellationToken cancellationToken = default);
    Task<LifecycleActionResult> SuspendTenantAsync(Guid id, string reason, CancellationToken cancellationToken = default);
    Task<LifecycleActionResult> ReactivateTenantAsync(Guid id, string reason, CancellationToken cancellationToken = default);
    Task<LifecycleActionResult> CancelTenantAsync(Guid id, string reason, CancellationToken cancellationToken = default);
    Task<LifecycleActionResult> ArchiveTenantAsync(Guid id, string reason, CancellationToken cancellationToken = default);
    Task<LifecycleActionResult> SetTenantProtectionAsync(Guid id, bool isProtected, string reason, CancellationToken cancellationToken = default);

    // Plan Catalog Methods
    Task<IReadOnlyCollection<PlanCatalogDto>?> GetPlansAsync(bool includeArchived = false, CancellationToken cancellationToken = default);
    Task<PlanCatalogDto?> GetPlanByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PlanCatalogDto?> CreatePlanAsync(CreatePlanCatalogCommand command, CancellationToken cancellationToken = default);
    Task<PlanVersionDto?> CreatePlanVersionAsync(Guid planId, CreatePlanVersionCommand command, CancellationToken cancellationToken = default);
    Task<PlanVersionDto?> UpdateDraftPlanVersionAsync(Guid versionId, UpdateDraftPlanVersionCommand command, CancellationToken cancellationToken = default);
    Task<PlanVersionDto?> PublishPlanVersionAsync(Guid versionId, string? approvalNotes = null, CancellationToken cancellationToken = default);
    Task<PlanVersionComparisonDto?> ComparePlanVersionsAsync(Guid baseVersionId, Guid targetVersionId, CancellationToken cancellationToken = default);
}

public sealed class LifecycleActionResult
{
    public TenantAdminDetailDto? Tenant { get; init; }
    public string? ErrorMessage { get; init; }
    public bool IsSuccess => Tenant is not null && string.IsNullOrWhiteSpace(ErrorMessage);
}

public sealed class ExportTenantsResult
{
    public byte[] Content { get; init; } = Array.Empty<byte>();
    public string FileName { get; init; } = "tenants-export.csv";
    public string? ErrorMessage { get; init; }
    public bool IsSuccess => string.IsNullOrWhiteSpace(ErrorMessage) && Content.Length > 0;
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

    public async Task<PagedTenantAdminResult?> GetTenantsAsync(
        string? searchTerm = null,
        string? status = null,
        string? subscribedPlan = null,
        string? state = null,
        string? ownerSearch = null,
        string? sizeSegment = null,
        string? commercialRegion = null,
        string? productiveProfile = null,
        string? churnRisk = null,
        IReadOnlyCollection<Guid>? tagIds = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        var url = BuildTenantFilterUrl("api/v1/backoffice/tenants", new TenantAdminFilterDto(
            searchTerm, status, subscribedPlan, state, ownerSearch,
            sizeSegment, commercialRegion, productiveProfile, churnRisk, tagIds), page, pageSize);
        return await _httpClient.GetFromJsonAsync<PagedTenantAdminResult>(url, cancellationToken);
    }

    public async Task<IReadOnlyCollection<OperationalTagAdminDto>?> GetOperationalTagsAsync(CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        return await _httpClient.GetFromJsonAsync<IReadOnlyCollection<OperationalTagAdminDto>>(
            "api/v1/backoffice/tenants/tags", cancellationToken);
    }

    public async Task<OperationalTagAdminDto?> CreateOperationalTagAsync(CreateOperationalTagAdminRequest request, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        var response = await _httpClient.PostAsJsonAsync("api/v1/backoffice/tenants/tags", request, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<OperationalTagAdminDto>(cancellationToken: cancellationToken);
    }

    public async Task<TenantAdminDetailDto?> UpdateTenantSegmentationAsync(Guid id, UpdateTenantSegmentationAdminRequest request, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        var response = await _httpClient.PutAsJsonAsync($"api/v1/backoffice/tenants/{id}/segmentation", request, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<TenantAdminDetailDto>(cancellationToken: cancellationToken);
    }

    public async Task<TenantAdminDetailDto?> ReplaceTenantTagsAsync(Guid id, ReplaceTenantTagsAdminRequest request, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        var response = await _httpClient.PutAsJsonAsync($"api/v1/backoffice/tenants/{id}/tags", request, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<TenantAdminDetailDto>(cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyCollection<AdminSavedFilterDto>?> GetSavedFiltersAsync(CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        return await _httpClient.GetFromJsonAsync<IReadOnlyCollection<AdminSavedFilterDto>>(
            "api/v1/backoffice/tenants/saved-filters", cancellationToken);
    }

    public async Task<AdminSavedFilterDto?> SaveFilterAsync(SaveAdminFilterRequest request, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        var response = await _httpClient.PostAsJsonAsync("api/v1/backoffice/tenants/saved-filters", request, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<AdminSavedFilterDto>(cancellationToken: cancellationToken);
    }

    public async Task<bool> DeleteSavedFilterAsync(Guid filterId, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        var response = await _httpClient.DeleteAsync($"api/v1/backoffice/tenants/saved-filters/{filterId}", cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<ExportTenantsResult?> ExportTenantsCsvAsync(TenantAdminFilterDto filter, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        var url = BuildTenantFilterUrl("api/v1/backoffice/tenants/export", filter);
        var response = await _httpClient.GetAsync(url, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var fileName = response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                ?? "tenants-export.csv";
            return new ExportTenantsResult { Content = bytes, FileName = fileName };
        }

        Error? error = null;
        try
        {
            error = await response.Content.ReadFromJsonAsync<Error>(cancellationToken: cancellationToken);
        }
        catch
        {
            // Ignore parse errors.
        }

        return new ExportTenantsResult { ErrorMessage = error?.Message ?? "Falha ao exportar recorte operacional." };
    }

    private static string BuildTenantFilterUrl(
        string basePath,
        TenantAdminFilterDto filter,
        int? page = null,
        int? pageSize = null)
    {
        var query = new List<string>();
        if (page.HasValue) query.Add($"page={page.Value}");
        if (pageSize.HasValue) query.Add($"pageSize={pageSize.Value}");
        if (!string.IsNullOrWhiteSpace(filter.SearchTerm)) query.Add($"searchTerm={Uri.EscapeDataString(filter.SearchTerm)}");
        if (!string.IsNullOrWhiteSpace(filter.Status)) query.Add($"status={Uri.EscapeDataString(filter.Status)}");
        if (!string.IsNullOrWhiteSpace(filter.SubscribedPlan)) query.Add($"subscribedPlan={Uri.EscapeDataString(filter.SubscribedPlan)}");
        if (!string.IsNullOrWhiteSpace(filter.State)) query.Add($"state={Uri.EscapeDataString(filter.State)}");
        if (!string.IsNullOrWhiteSpace(filter.OwnerSearch)) query.Add($"ownerSearch={Uri.EscapeDataString(filter.OwnerSearch)}");
        if (!string.IsNullOrWhiteSpace(filter.SizeSegment)) query.Add($"sizeSegment={Uri.EscapeDataString(filter.SizeSegment)}");
        if (!string.IsNullOrWhiteSpace(filter.CommercialRegion)) query.Add($"commercialRegion={Uri.EscapeDataString(filter.CommercialRegion)}");
        if (!string.IsNullOrWhiteSpace(filter.ProductiveProfile)) query.Add($"productiveProfile={Uri.EscapeDataString(filter.ProductiveProfile)}");
        if (!string.IsNullOrWhiteSpace(filter.ChurnRisk)) query.Add($"churnRisk={Uri.EscapeDataString(filter.ChurnRisk)}");
        if (filter.TagIds is { Count: > 0 })
        {
            query.AddRange(filter.TagIds.Select(tagId => $"tagIds={tagId}"));
        }

        return query.Count == 0 ? basePath : $"{basePath}?{string.Join('&', query)}";
    }

    public async Task<TenantAdminDetailDto?> GetTenantByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        return await _httpClient.GetFromJsonAsync<TenantAdminDetailDto>($"api/v1/backoffice/tenants/{id}", cancellationToken);
    }

    public async Task<TenantAdminDetailDto?> CreateTenantAsync(CreateTenantAdminRequest request, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        var response = await _httpClient.PostAsJsonAsync("api/v1/backoffice/tenants", request, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<TenantAdminDetailDto>(cancellationToken: cancellationToken);
    }

    public async Task<TenantAdminDetailDto?> UpdateTenantAsync(Guid id, UpdateTenantAdminRequest request, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        var response = await _httpClient.PutAsJsonAsync($"api/v1/backoffice/tenants/{id}", request, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<TenantAdminDetailDto>(cancellationToken: cancellationToken);
    }

    public Task<LifecycleActionResult> SuspendTenantAsync(Guid id, string reason, CancellationToken cancellationToken = default) =>
        PostLifecycleActionAsync($"api/v1/backoffice/tenants/{id}/suspend", new TenantLifecycleActionRequest(reason), cancellationToken);

    public Task<LifecycleActionResult> ReactivateTenantAsync(Guid id, string reason, CancellationToken cancellationToken = default) =>
        PostLifecycleActionAsync($"api/v1/backoffice/tenants/{id}/reactivate", new TenantLifecycleActionRequest(reason), cancellationToken);

    public Task<LifecycleActionResult> CancelTenantAsync(Guid id, string reason, CancellationToken cancellationToken = default) =>
        PostLifecycleActionAsync($"api/v1/backoffice/tenants/{id}/cancel", new TenantLifecycleActionRequest(reason), cancellationToken);

    public Task<LifecycleActionResult> ArchiveTenantAsync(Guid id, string reason, CancellationToken cancellationToken = default) =>
        PostLifecycleActionAsync($"api/v1/backoffice/tenants/{id}/archive", new TenantLifecycleActionRequest(reason), cancellationToken);

    public Task<LifecycleActionResult> SetTenantProtectionAsync(Guid id, bool isProtected, string reason, CancellationToken cancellationToken = default) =>
        PostLifecycleActionAsync($"api/v1/backoffice/tenants/{id}/protection", new TenantProtectionRequest(isProtected, reason), cancellationToken);

    private async Task<LifecycleActionResult> PostLifecycleActionAsync<TRequest>(string url, TRequest request, CancellationToken cancellationToken)
    {
        await AttachTokenAsync();
        var response = await _httpClient.PostAsJsonAsync(url, request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var tenant = await response.Content.ReadFromJsonAsync<TenantAdminDetailDto>(cancellationToken: cancellationToken);
            return new LifecycleActionResult { Tenant = tenant };
        }

        Error? error = null;
        try
        {
            error = await response.Content.ReadFromJsonAsync<Error>(cancellationToken: cancellationToken);
        }
        catch
        {
            // Ignore parse errors.
        }

        return new LifecycleActionResult
        {
            ErrorMessage = error?.Message ?? "Não foi possível executar a ação de ciclo de vida."
        };
    }

    public async Task<IReadOnlyCollection<PlanCatalogDto>?> GetPlansAsync(bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        return await _httpClient.GetFromJsonAsync<IReadOnlyCollection<PlanCatalogDto>>($"api/v1/backoffice/plans?includeArchived={includeArchived}", cancellationToken);
    }

    public async Task<PlanCatalogDto?> GetPlanByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        return await _httpClient.GetFromJsonAsync<PlanCatalogDto>($"api/v1/backoffice/plans/{id}", cancellationToken);
    }

    public async Task<PlanCatalogDto?> CreatePlanAsync(CreatePlanCatalogCommand command, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        var response = await _httpClient.PostAsJsonAsync("api/v1/backoffice/plans", command, cancellationToken);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<PlanCatalogDto>(cancellationToken: cancellationToken) : null;
    }

    public async Task<PlanVersionDto?> CreatePlanVersionAsync(Guid planId, CreatePlanVersionCommand command, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        var response = await _httpClient.PostAsJsonAsync($"api/v1/backoffice/plans/{planId}/versions", command, cancellationToken);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<PlanVersionDto>(cancellationToken: cancellationToken) : null;
    }

    public async Task<PlanVersionDto?> UpdateDraftPlanVersionAsync(Guid versionId, UpdateDraftPlanVersionCommand command, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        var response = await _httpClient.PutAsJsonAsync($"api/v1/backoffice/plans/versions/{versionId}", command, cancellationToken);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<PlanVersionDto>(cancellationToken: cancellationToken) : null;
    }

    public async Task<PlanVersionDto?> PublishPlanVersionAsync(Guid versionId, string? approvalNotes = null, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        var response = await _httpClient.PostAsJsonAsync($"api/v1/backoffice/plans/versions/{versionId}/publish", new { ApprovalNotes = approvalNotes }, cancellationToken);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<PlanVersionDto>(cancellationToken: cancellationToken) : null;
    }

    public async Task<PlanVersionComparisonDto?> ComparePlanVersionsAsync(Guid baseVersionId, Guid targetVersionId, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();
        return await _httpClient.GetFromJsonAsync<PlanVersionComparisonDto>($"api/v1/backoffice/plans/versions/compare?baseVersionId={baseVersionId}&targetVersionId={targetVersionId}", cancellationToken);
    }
}
