using CriaCerto.Shared.UI.Abstractions;

namespace CriaCerto.Mobile.Services;

/// <summary>
/// Monitor de conectividade nativo para .NET MAUI.
/// Assina os eventos reativos de Connectivity.Current para propagar status online/offline na UI.
/// </summary>
public sealed class MauiConnectivityService : IConnectivityService, IDisposable
{
    private bool _isSubscribed;

    public bool IsOnline => Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

    public event Action<bool>? ConnectivityChanged;

    public Task InitializeAsync()
    {
        if (!_isSubscribed)
        {
            Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
            _isSubscribed = true;
        }

        return Task.CompletedTask;
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        var online = e.NetworkAccess == NetworkAccess.Internet;
        ConnectivityChanged?.Invoke(online);
    }

    public void Dispose()
    {
        if (_isSubscribed)
        {
            Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
            _isSubscribed = false;
        }
    }
}

