namespace CriaCerto.BuildingBlocks.Abstractions.Security;

/// <summary>
/// Opções de configuração para criptografia de dados em repouso e blind indexing.
/// </summary>
public sealed class EncryptionOptions
{
    public const string SectionName = "Encryption";

    /// <summary>
    /// Chave primária ativa para encriptação (chave de 256 bits codificada em Base64).
    /// </summary>
    public string CurrentKey { get; set; } = string.Empty;

    /// <summary>
    /// Versão numérica da chave primária ativa (padrão: 1).
    /// </summary>
    public byte CurrentKeyVersion { get; set; } = 1;

    /// <summary>
    /// Chave secreta / pepper utilizada para derivar o hash cego (blind index) determinístico via HMAC-SHA256 (Base64).
    /// </summary>
    public string BlindIndexKey { get; set; } = string.Empty;

    /// <summary>
    /// Chaveiro de versões de chaves anteriores para decifração durante rotação de chaves: [Versão] = Base64Key.
    /// </summary>
    public Dictionary<byte, string> KeyRing { get; set; } = new();
}
