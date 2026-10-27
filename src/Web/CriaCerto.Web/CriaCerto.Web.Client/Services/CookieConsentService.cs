using System.Text.Json;
using CriaCerto.Web.Client.Models;
using Microsoft.JSInterop;

namespace CriaCerto.Web.Client.Services;

public interface ICookieConsentService
{
    event Action? OnConsentChanged;
    ValueTask<bool> HasValidConsentAsync();
    ValueTask<CookieConsentState?> GetConsentAsync();
    ValueTask SaveConsentAsync(CookieConsentState? customState = null);
    ValueTask ResetConsentAsync();
}

public class CookieConsentService : ICookieConsentService
{
    private readonly IJSRuntime _jsRuntime;
    private CookieConsentState? _cachedState;
    private bool _hasChecked;

    public event Action? OnConsentChanged;

    public CookieConsentService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async ValueTask<bool> HasValidConsentAsync()
    {
        var consent = await GetConsentAsync();
        return consent?.IsValid() ?? false;
    }

    public async ValueTask<CookieConsentState?> GetConsentAsync()
    {
        if (_hasChecked && _cachedState != null)
        {
            return _cachedState;
        }

        try
        {
            var json = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", CookieConsentCatalog.StorageKey);
            if (string.IsNullOrWhiteSpace(json))
            {
                _cachedState = null;
                _hasChecked = true;
                return null;
            }

            var state = JsonSerializer.Deserialize<CookieConsentState>(json);
            _cachedState = state;
            _hasChecked = true;
            return state;
        }
        catch
        {
            _cachedState = null;
            _hasChecked = true;
            return null;
        }
    }

    public async ValueTask SaveConsentAsync(CookieConsentState? customState = null)
    {
        var state = customState ?? CookieConsentState.CreateDefaultAccepted();
        var json = JsonSerializer.Serialize(state);

        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", CookieConsentCatalog.StorageKey, json);
        }
        catch
        {
            // Ignorado em caso de bloqueio estrito de armazenamento local pelo browser
        }

        try
        {
            if (state.AnalyticsTracking)
            {
                await _jsRuntime.InvokeVoidAsync("grantAnalyticsConsent");
            }
            else
            {
                await _jsRuntime.InvokeVoidAsync("revokeAnalyticsConsent");
            }
        }
        catch
        {
            // Resiliente se scripts globais não estiverem disponíveis
        }

        _cachedState = state;
        _hasChecked = true;
        OnConsentChanged?.Invoke();
    }

    public async ValueTask ResetConsentAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", CookieConsentCatalog.StorageKey);
        }
        catch
        {
            // Resiliente
        }

        try
        {
            await _jsRuntime.InvokeVoidAsync("revokeAnalyticsConsent");
        }
        catch
        {
            // Resiliente
        }

        _cachedState = null;
        _hasChecked = true;
        OnConsentChanged?.Invoke();
    }
}
