namespace CriaCerto.BuildingBlocks.Abstractions.Security;

/// <summary>
/// Contrato para criptografia simétrica autenticada em repouso (LGPD Art. 46)
/// com suporte a Envelope AEAD (AES-256-GCM), rotação de chaves e blind indexing determinístico.
/// </summary>
public interface IDataEncryptionService
{
    /// <summary>
    /// Criptografa o texto plano utilizando a chave ativa atual e AES-256-GCM,
    /// retornando o payload canônico codificado em Base64 com prefixo de envelope ($cc1$).
    /// </summary>
    string Encrypt(string plainText);

    /// <summary>
    /// Decifra o payload criptografado. Se o valor estiver em texto puro (legado),
    /// retorna o próprio valor graciosamente para compatibilidade em transições de dados.
    /// </summary>
    string Decrypt(string cipherText);

    /// <summary>
    /// Computa um hash cego (blind index) determinístico via HMAC-SHA256 para permitir
    /// buscas exatas indexadas e restrições de unicidade sem expor o PII original.
    /// </summary>
    string ComputeBlindIndex(string plainText);
}
