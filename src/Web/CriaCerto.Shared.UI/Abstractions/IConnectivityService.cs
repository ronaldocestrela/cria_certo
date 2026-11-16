namespace CriaCerto.Shared.UI.Abstractions;

/// <summary>
/// Monitor de conectividade e status de rede da plataforma.
/// Suporta detecção via browser (window online/offline / navigator.onLine) e APIs nativas (.NET MAUI Connectivity).
/// </summary>
public interface IConnectivityService
{
    bool IsOnline { get; }
    event Action<bool>? ConnectivityChanged;
    Task InitializeAsync();
}

