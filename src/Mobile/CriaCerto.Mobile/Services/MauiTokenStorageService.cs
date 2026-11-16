using CriaCerto.Shared.UI.Abstractions;
using Microsoft.Extensions.Logging;

namespace CriaCerto.Mobile.Services;

/// <summary>
/// Provedor nativo de armazenamento seguro de token JWT para .NET MAUI.
/// Utiliza o SecureStorage da plataforma (Android KeyStore / iOS Keychain) com fallback em memória resiliente.
/// </summary>
public class MauiTokenStorageService : ITokenStorageService
{
    private const string TokenKey = "authToken";
    private readonly ILogger<MauiTokenStorageService>? _logger;
    private string? _fallbackToken;

    public MauiTokenStorageService(ILogger<MauiTokenStorageService>? logger = null)
    {
        _logger = logger;
    }

    public async Task<string?> GetTokenAsync()
    {
        try
        {
            return await SecureStorage.Default.GetAsync(TokenKey) ?? _fallbackToken;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Falha ao recuperar token do SecureStorage nativo. Utilizando fallback em memória.");
            return _fallbackToken;
        }
    }

    public async Task SetTokenAsync(string token)
    {
        try
        {
            await SecureStorage.Default.SetAsync(TokenKey, token);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Falha ao persistir token no SecureStorage nativo. Armazenando em fallback.");
        }
        finally
        {
            _fallbackToken = token;
        }
    }

    public Task RemoveTokenAsync()
    {
        try
        {
            SecureStorage.Default.Remove(TokenKey);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Falha ao remover token do SecureStorage nativo.");
        }
        finally
        {
            _fallbackToken = null;
        }

        return Task.CompletedTask;
    }
}

