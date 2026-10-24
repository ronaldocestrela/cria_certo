using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Web.Client.Models;
using Microsoft.JSInterop;

namespace CriaCerto.Web.Client.Services;

public interface IPartnerApiClient
{
    Task<Result<PartnerPortalDashboardModel>> GetDashboardAsync(CancellationToken cancellationToken = default);
    Task<Result<PartnerCommissionStatementModel>> GetStatementAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        int? status = null,
        string? search = null,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);
    Task<Result> UpdatePixKeyAsync(int pixKeyType, string pixKey, CancellationToken cancellationToken = default);
    Task<Result<PartnerCampaignSummaryModel>> CreateCampaignAsync(string campaignTitle, string? preferredSlug, CancellationToken cancellationToken = default);
}

public sealed class PartnerApiClient : IPartnerApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IJSRuntime _jsRuntime;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public PartnerApiClient(HttpClient httpClient, IJSRuntime jsRuntime)
    {
        _httpClient = httpClient;
        _jsRuntime = jsRuntime;
    }

    public async Task<Result<PartnerPortalDashboardModel>> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();

        try
        {
            var response = await _httpClient.GetAsync("/api/v1/referral/partner/dashboard", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadFromJsonAsync<PartnerPortalDashboardModel>(JsonOptions, cancellationToken);
                return content != null
                    ? Result.Success(content)
                    : Result.Failure<PartnerPortalDashboardModel>(Error.Failure("Partner.EmptyPayload", "Não foi possível carregar os dados do parceiro."));
            }

            return await ExtractErrorResultAsync<PartnerPortalDashboardModel>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<PartnerPortalDashboardModel>(Error.Failure("Partner.NetworkError", $"Erro de comunicação com o servidor: {ex.Message}"));
        }
    }

    public async Task<Result<PartnerCommissionStatementModel>> GetStatementAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        int? status = null,
        string? search = null,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();

        try
        {
            var queryParams = new List<string>
            {
                $"page={page}",
                $"pageSize={pageSize}"
            };

            if (startDate.HasValue) queryParams.Add($"startDate={Uri.EscapeDataString(startDate.Value.ToString("O"))}");
            if (endDate.HasValue) queryParams.Add($"endDate={Uri.EscapeDataString(endDate.Value.ToString("O"))}");
            if (status.HasValue) queryParams.Add($"status={status.Value}");
            if (!string.IsNullOrWhiteSpace(search)) queryParams.Add($"search={Uri.EscapeDataString(search.Trim())}");

            var url = $"/api/v1/referral/partner/statement?{string.Join("&", queryParams)}";
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadFromJsonAsync<PartnerCommissionStatementModel>(JsonOptions, cancellationToken);
                return content != null
                    ? Result.Success(content)
                    : Result.Failure<PartnerCommissionStatementModel>(Error.Failure("Partner.EmptyPayload", "Extrato vazio retornado pelo servidor."));
            }

            return await ExtractErrorResultAsync<PartnerCommissionStatementModel>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<PartnerCommissionStatementModel>(Error.Failure("Partner.NetworkError", $"Erro de comunicação com o servidor: {ex.Message}"));
        }
    }

    public async Task<Result> UpdatePixKeyAsync(int pixKeyType, string pixKey, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();

        try
        {
            var request = new UpdatePartnerPixKeyRequestModel(pixKeyType, pixKey);
            var response = await _httpClient.PutAsJsonAsync("/api/v1/referral/partner/pix-key", request, JsonOptions, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return Result.Success();
            }

            var errorResult = await ExtractErrorResultAsync<object>(response, cancellationToken);
            return Result.Failure(errorResult.Error);
        }
        catch (Exception ex)
        {
            return Result.Failure(Error.Failure("Partner.NetworkError", $"Erro de comunicação com o servidor: {ex.Message}"));
        }
    }

    public async Task<Result<PartnerCampaignSummaryModel>> CreateCampaignAsync(string campaignTitle, string? preferredSlug, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();

        try
        {
            var request = new CreatePartnerCampaignCodeRequestModel(campaignTitle, preferredSlug);
            var response = await _httpClient.PostAsJsonAsync("/api/v1/referral/partner/campaigns", request, JsonOptions, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadFromJsonAsync<PartnerCampaignSummaryModel>(JsonOptions, cancellationToken);
                return content != null
                    ? Result.Success(content)
                    : Result.Failure<PartnerCampaignSummaryModel>(Error.Failure("Partner.EmptyPayload", "Resposta vazia ao criar campanha."));
            }

            return await ExtractErrorResultAsync<PartnerCampaignSummaryModel>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<PartnerCampaignSummaryModel>(Error.Failure("Partner.NetworkError", $"Erro de comunicação com o servidor: {ex.Message}"));
        }
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

    private static async Task<Result<T>> ExtractErrorResultAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var error = await response.Content.ReadFromJsonAsync<Error>(JsonOptions, cancellationToken);
            if (error != null && !string.IsNullOrWhiteSpace(error.Message))
            {
                return Result.Failure<T>(error);
            }
        }
        catch
        {
            // Ignorar falha de deserialização do payload de erro
        }

        return Result.Failure<T>(Error.Failure(
            $"Http.{(int)response.StatusCode}",
            $"Falha na operação ({response.StatusCode}). Tente novamente mais tarde."));
    }
}
