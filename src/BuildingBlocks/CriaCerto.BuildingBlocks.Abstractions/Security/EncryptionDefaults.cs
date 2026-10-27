namespace CriaCerto.BuildingBlocks.Abstractions.Security;

/// <summary>
/// Valores padrão e constantes para criptografia em repouso e ambientes de desenvolvimento/testes.
/// </summary>
public static class EncryptionDefaults
{
    public const string EnvelopePrefix = "$cc1$";

    /// <summary>
    /// Chave simétrica padrão de desenvolvimento (32 bytes / 256 bits codificada em Base64).
    /// </summary>
    public const string DefaultDevKey = "4rQZ2wX1/kE9hY9vP5l7m3o1Qz7wX1/kE9hY9vP5l7k=";

    /// <summary>
    /// Chave padrão de desenvolvimento para HMAC-SHA256 Blind Index (32 bytes em Base64).
    /// </summary>
    public const string DefaultDevBlindIndexKey = "8xTZ3wY2/mE0hZ0vQ6l8m4o2Qz8wY2/mE0hZ0vQ6l8k=";
}
