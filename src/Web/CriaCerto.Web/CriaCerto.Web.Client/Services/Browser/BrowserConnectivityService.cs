using CriaCerto.Shared.UI.Abstractions;
using Microsoft.JSInterop;

namespace CriaCerto.Web.Client.Services.Browser;

/// <summary>
/// Implementação browser de IConnectivityService usando eventos online/offline da janela e navigator.onLine.
/// </summary>
public sealed class BrowserConnectivityService : IConnectivityService, IDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private DotNetObjectReference<BrowserConnectivityService>? _dotNetRef;
    private bool _initialized;

    public bool IsOnline { get; private set; } = true;
    public event Action<bool>? ConnectivityChanged;

    public BrowserConnectivityService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        if (!OperatingSystem.IsBrowser())
        {
            IsOnline = true;
            _initialized = true;
            return;
        }

        try
        {
            _dotNetRef = DotNetObjectReference.Create(this);
            IsOnline = await _jsRuntime.InvokeAsync<bool>("criaCertoOfflineSync.init", _dotNetRef);
            _initialized = true;
        }
        catch
        {
            IsOnline = true;
            _initialized = true;
        }
    }

    [JSInvokable]
    public void OnNetworkStatusChanged(bool isOnline)
    {
        IsOnline = isOnline;
        ConnectivityChanged?.Invoke(isOnline);
    }

    public void Dispose()
    {
        _dotNetRef?.Dispose();
    }
}

