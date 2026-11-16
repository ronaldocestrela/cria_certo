using CriaCerto.Shared.UI.Abstractions;
using Microsoft.JSInterop;

namespace CriaCerto.Shared.UI.Services;

/// <summary>
/// Adaptador transitivo que encapsula IJSRuntime para fornecer ITokenStorageService,
/// preservando retrocompatibilidade com testes e componentes legados.
/// </summary>
internal sealed class JSRuntimeTokenStorageAdapter : ITokenStorageService
{
    private const string TokenStorageKey = "authToken";
    private readonly IJSRuntime _jsRuntime;

    public JSRuntimeTokenStorageAdapter(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<string?> GetTokenAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", TokenStorageKey);
        }
        catch
        {
            return null;
        }
    }

    public async Task SetTokenAsync(string token)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", TokenStorageKey, token);
        }
        catch
        {
        }
    }

    public async Task RemoveTokenAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", TokenStorageKey);
        }
        catch
        {
        }
    }
}

