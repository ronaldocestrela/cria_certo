namespace CriaCerto.BuildingBlocks.Abstractions.Security;

/// <summary>
/// Implementação de fallback para ambientes de teste e contextos sem infraestrutura de criptografia configurada.
/// </summary>
public sealed class NullDataEncryptionService : IDataEncryptionService
{
    public static readonly NullDataEncryptionService Instance = new();

    public string Encrypt(string plainText) => plainText;

    public string Decrypt(string cipherText) => cipherText;

    public string ComputeBlindIndex(string plainText)
    {
        if (string.IsNullOrWhiteSpace(plainText))
        {
            return string.Empty;
        }

        var clean = plainText.Trim().ToLowerInvariant();
        var bytes = System.Text.Encoding.UTF8.GetBytes(clean);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
