using System.Net.Http.Json;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using Microsoft.JSInterop;

namespace CriaCerto.Web.Client.Services;

public sealed class ReferralTrackingService : IReferralTrackingService
{
    private const string ReferralStorageKey = "cc_referral_code";
    private readonly IJSRuntime _jsRuntime;
    private readonly HttpClient _http;
    private string? _cachedCode;

    public ReferralTrackingService(IJSRuntime jsRuntime, HttpClient http)
    {
        _jsRuntime = jsRuntime;
        _http = http;
    }

    public async Task CaptureReferralCodeAsync(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return;
        }

        var normalizedCode = code.Trim().ToUpperInvariant();
        _cachedCode = normalizedCode;

        if (OperatingSystem.IsBrowser())
        {
            try
            {
                // Salva no LocalStorage
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", ReferralStorageKey, normalizedCode);

                // Salva em Cookie seguro (SameSite=Lax, 30 dias de retenção)
                var cookieScript = $"document.cookie = '{ReferralStorageKey}=' + encodeURIComponent('{normalizedCode}') + '; path=/; max-age=2592000; SameSite=Lax'";
                await _jsRuntime.InvokeVoidAsync("eval", cookieScript);
            }
            catch
            {
                // Fallback silencioso caso JS interop não esteja disponível
            }
        }

        // Dispara de forma assíncrona o registro de clique no backend (não-bloqueante)
        _ = Task.Run(async () =>
        {
            try
            {
                await _http.PostAsync($"/api/v1/referral/clicks/{Uri.EscapeDataString(normalizedCode)}", null);
            }
            catch
            {
                // Ignora falhas de rede no registro não-crítico de cliques
            }
        });
    }

    public async Task<string?> GetReferralCodeAsync()
    {
        if (!string.IsNullOrWhiteSpace(_cachedCode))
        {
            return _cachedCode;
        }

        if (!OperatingSystem.IsBrowser())
        {
            return null;
        }

        try
        {
            // 1. Tenta recuperar do LocalStorage
            var stored = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", ReferralStorageKey);
            if (!string.IsNullOrWhiteSpace(stored))
            {
                _cachedCode = stored.Trim().ToUpperInvariant();
                return _cachedCode;
            }

            // 2. Fallback: tenta recuperar dos Cookies
            var cookies = await _jsRuntime.InvokeAsync<string?>("eval", "document.cookie");
            if (!string.IsNullOrWhiteSpace(cookies))
            {
                var cookieParts = cookies.Split(';');
                foreach (var part in cookieParts)
                {
                    var trimmed = part.Trim();
                    if (trimmed.StartsWith($"{ReferralStorageKey}=", StringComparison.OrdinalIgnoreCase))
                    {
                        var value = trimmed.Substring(ReferralStorageKey.Length + 1);
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            _cachedCode = Uri.UnescapeDataString(value).Trim().ToUpperInvariant();
                            return _cachedCode;
                        }
                    }
                }
            }
        }
        catch
        {
            // Fallback para nulo em caso de erro no JS Interop
        }

        return null;
    }

    public async Task ClearReferralCodeAsync()
    {
        _cachedCode = null;

        if (OperatingSystem.IsBrowser())
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", ReferralStorageKey);
                var expireCookieScript = $"document.cookie = '{ReferralStorageKey}=; path=/; max-age=0; SameSite=Lax'";
                await _jsRuntime.InvokeVoidAsync("eval", expireCookieScript);
            }
            catch
            {
                // Fallback silencioso
            }
        }
    }

    public async Task<ReferralValidationResult> ValidateReferralCodeAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return new ReferralValidationResult(false, null, null, null, "Código não informado.");
        }

        var normalizedCode = code.Trim().ToUpperInvariant();

        try
        {
            var response = await _http.GetAsync($"/api/v1/referral/codes/validate/{Uri.EscapeDataString(normalizedCode)}");
            if (response.IsSuccessStatusCode)
            {
                var dto = await response.Content.ReadFromJsonAsync<ReferralCodeApiDto>();
                if (dto != null)
                {
                    return new ReferralValidationResult(
                        IsValid: true,
                        Code: dto.Code,
                        CampaignTitle: dto.CampaignTitle,
                        ParticipantType: dto.ParticipantType,
                        ErrorMessage: null
                    );
                }
            }

            try
            {
                var err = await response.Content.ReadFromJsonAsync<Error>();
                return new ReferralValidationResult(false, normalizedCode, null, null, err?.Message ?? "Código de indicação inválido.");
            }
            catch
            {
                return new ReferralValidationResult(false, normalizedCode, null, null, "Código de indicação inválido ou não encontrado.");
            }
        }
        catch (Exception ex)
        {
            return new ReferralValidationResult(false, normalizedCode, null, null, $"Erro ao validar código: {ex.Message}");
        }
    }

    private sealed record ReferralCodeApiDto(string Code, bool IsValid, string? CampaignTitle, string ParticipantType);
}
