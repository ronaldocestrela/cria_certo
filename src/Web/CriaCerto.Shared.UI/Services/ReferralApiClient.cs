using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Shared.UI.Abstractions;
using CriaCerto.Shared.UI.Models;
using Microsoft.JSInterop;

namespace CriaCerto.Shared.UI.Services;

public interface IReferralApiClient
{
    Task<Result<CustomerReferralDashboardModel>> GetCustomerReferralDashboardAsync(CancellationToken cancellationToken = default);
    Task<Result<CustomizeReferralCodeResponseModel>> CustomizeReferralCodeAsync(string newCode, CancellationToken cancellationToken = default);
}

public sealed class ReferralApiClient : IReferralApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ITokenStorageService _tokenStorage;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ReferralApiClient(HttpClient httpClient, ITokenStorageService tokenStorage)
    {
        _httpClient = httpClient;
        _tokenStorage = tokenStorage;
    }

    public ReferralApiClient(HttpClient httpClient, IJSRuntime jsRuntime)
        : this(httpClient, new JSRuntimeTokenStorageAdapter(jsRuntime))
    {
    }

    public async Task<Result<CustomerReferralDashboardModel>> GetCustomerReferralDashboardAsync(CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();

        try
        {
            var response = await _httpClient.GetAsync("/api/v1/referral/customer/dashboard", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadFromJsonAsync<CustomerReferralDashboardModel>(JsonOptions, cancellationToken);
                return content != null
                    ? Result.Success(content)
                    : Result.Failure<CustomerReferralDashboardModel>(Error.Failure("Referral.EmptyPayload", "Não foi possível carregar os dados de indicação."));
            }

            return await ExtractErrorResultAsync<CustomerReferralDashboardModel>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<CustomerReferralDashboardModel>(Error.Failure("Referral.NetworkError", $"Erro de comunicação com o servidor: {ex.Message}"));
        }
    }

    public async Task<Result<CustomizeReferralCodeResponseModel>> CustomizeReferralCodeAsync(string newCode, CancellationToken cancellationToken = default)
    {
        await AttachTokenAsync();

        try
        {
            var request = new CustomizeReferralCodeRequestModel(newCode);
            var response = await _httpClient.PostAsJsonAsync("/api/v1/referral/customer/code/customize", request, JsonOptions, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadFromJsonAsync<CustomizeReferralCodeResponseModel>(JsonOptions, cancellationToken);
                return content != null
                    ? Result.Success(content)
                    : Result.Failure<CustomizeReferralCodeResponseModel>(Error.Failure("Referral.EmptyPayload", "Resposta vazia ao personalizar código."));
            }

            return await ExtractErrorResultAsync<CustomizeReferralCodeResponseModel>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<CustomizeReferralCodeResponseModel>(Error.Failure("Referral.NetworkError", $"Erro de comunicação com o servidor: {ex.Message}"));
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
            // Ignorar falha ao desserializar erro estruturado
        }

        return Result.Failure<T>(Error.Failure(
            $"Http.{(int)response.StatusCode}",
            $"Falha na operação ({response.StatusCode}). Tente novamente mais tarde."));
    }
}
