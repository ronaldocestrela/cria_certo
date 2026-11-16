namespace CriaCerto.Shared.UI.Abstractions;

/// <summary>
/// Provedor desacoplado de armazenamento de token de autenticação (JWT).
/// Suporta implementações em Browser (localStorage / cookie) e Mobile (.NET MAUI SecureStorage).
/// </summary>
public interface ITokenStorageService
{
    Task<string?> GetTokenAsync();
    Task SetTokenAsync(string token);
    Task RemoveTokenAsync();
}

