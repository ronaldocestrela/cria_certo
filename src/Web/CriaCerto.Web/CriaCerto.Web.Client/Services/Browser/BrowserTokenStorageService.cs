using CriaCerto.Shared.UI.Abstractions;
using Microsoft.JSInterop;

namespace CriaCerto.Web.Client.Services.Browser;

/// <summary>
/// Implementação browser de ITokenStorageService via localStorage do navegador.
/// Protegido contra execuções no lado servidor (SSR) e exceções de JS Interop.
/// </summary>
public class BrowserTokenStorageService : ITokenStorageService
{
    private const string TokenStorageKey = "authToken";
    private readonly IJSRuntime _jsRuntime;

    public BrowserTokenStorageService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<string?> GetTokenAsync()
    {
        if (!OperatingSystem.IsBrowser())
        {
            return null;
        }

        try
        {
            return await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", TokenStorageKey);
        }
        catch (JSException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    public async Task SetTokenAsync(string token)
    {
        if (!OperatingSystem.IsBrowser())
        {
            return;
        }

        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", TokenStorageKey, token);
        }
        catch (JSException)
        {
            // Tratamento silencioso caso o storage esteja restrito
        }
        catch (InvalidOperationException)
        {
            // Tratamento silencioso durante prerender
        }
    }

    public async Task RemoveTokenAsync()
    {
        if (!OperatingSystem.IsBrowser())
        {
            return;
        }

        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", TokenStorageKey);
        }
        catch (JSException)
        {
            // Tratamento silencioso
        }
        catch (InvalidOperationException)
        {
            // Tratamento silencioso
        }
    }
}

