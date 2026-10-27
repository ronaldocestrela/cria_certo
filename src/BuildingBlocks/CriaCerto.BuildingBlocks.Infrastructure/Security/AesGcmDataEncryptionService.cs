using System.Security.Cryptography;
using System.Text;
using CriaCerto.BuildingBlocks.Abstractions.Security;
using Microsoft.Extensions.Options;

namespace CriaCerto.BuildingBlocks.Infrastructure.Security;

/// <summary>
/// Implementação de alta performance e segurança de criptografia simétrica em repouso
/// baseada em AES-256-GCM (Galois/Counter Mode) e HMAC-SHA256 para Blind Indexing (LGPD Art. 46).
/// </summary>
public sealed class AesGcmDataEncryptionService : IDataEncryptionService
{
    private const int NonceSizeInBytes = 12; // 96 bits recomendado pelo NIST SP 800-38D
    private const int TagSizeInBytes = 16;   // 128 bits auth tag
    private const int HeaderSizeInBytes = 1 + NonceSizeInBytes + TagSizeInBytes; // 29 bytes

    private readonly byte _currentKeyVersion;
    private readonly byte[] _currentKeyBytes;
    private readonly byte[] _blindIndexKeyBytes;
    private readonly Dictionary<byte, byte[]> _keyRing = new();

    public AesGcmDataEncryptionService(IOptions<EncryptionOptions> options)
        : this(options.Value)
    {
    }

    public AesGcmDataEncryptionService(EncryptionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _currentKeyVersion = options.CurrentKeyVersion <= 0 ? (byte)1 : options.CurrentKeyVersion;

        var keyBase64 = !string.IsNullOrWhiteSpace(options.CurrentKey)
            ? options.CurrentKey
            : EncryptionDefaults.DefaultDevKey;

        _currentKeyBytes = Convert.FromBase64String(keyBase64);
        if (_currentKeyBytes.Length != 32)
        {
            throw new ArgumentException("A chave de criptografia AES-256 deve ter exatamente 256 bits (32 bytes).", nameof(options));
        }

        var blindKeyBase64 = !string.IsNullOrWhiteSpace(options.BlindIndexKey)
            ? options.BlindIndexKey
            : EncryptionDefaults.DefaultDevBlindIndexKey;

        _blindIndexKeyBytes = Convert.FromBase64String(blindKeyBase64);

        // Registra a chave ativa no chaveiro
        _keyRing[_currentKeyVersion] = _currentKeyBytes;

        // Registra chaves históricas do chaveiro
        if (options.KeyRing is { Count: > 0 })
        {
            foreach (var (version, b64) in options.KeyRing)
            {
                var bytes = Convert.FromBase64String(b64);
                if (bytes.Length == 32)
                {
                    _keyRing[version] = bytes;
                }
            }
        }
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return plainText;
        }

        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var nonce = new byte[NonceSizeInBytes];
        RandomNumberGenerator.Fill(nonce);

        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[TagSizeInBytes];

        using (var aesGcm = new AesGcm(_currentKeyBytes, TagSizeInBytes))
        {
            aesGcm.Encrypt(nonce, plainBytes, cipherBytes, tag);
        }

        // Monta o payload binário: [Version (1B)] [Nonce (12B)] [Tag (16B)] [Ciphertext (NB)]
        var payload = new byte[HeaderSizeInBytes + cipherBytes.Length];
        payload[0] = _currentKeyVersion;
        Buffer.BlockCopy(nonce, 0, payload, 1, NonceSizeInBytes);
        Buffer.BlockCopy(tag, 0, payload, 1 + NonceSizeInBytes, TagSizeInBytes);
        Buffer.BlockCopy(cipherBytes, 0, payload, HeaderSizeInBytes, cipherBytes.Length);

        return EncryptionDefaults.EnvelopePrefix + Convert.ToBase64String(payload);
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
        {
            return cipherText;
        }

        // Se não possui o prefixo do envelope $cc1$, trata como dado legado não-cifrado (plain text)
        if (!cipherText.StartsWith(EncryptionDefaults.EnvelopePrefix, StringComparison.Ordinal))
        {
            return cipherText;
        }

        var base64Part = cipherText.Substring(EncryptionDefaults.EnvelopePrefix.Length);
        byte[] payload;
        try
        {
            payload = Convert.FromBase64String(base64Part);
        }
        catch (FormatException)
        {
            // Se falhar Base64, retorna como texto puro para evitar crash
            return cipherText;
        }

        if (payload.Length < HeaderSizeInBytes)
        {
            return cipherText;
        }

        var keyVersion = payload[0];
        if (!_keyRing.TryGetValue(keyVersion, out var keyBytes))
        {
            throw new CryptographicException($"Chave de descriptografia versão '{keyVersion}' não encontrada no chaveiro ativo.");
        }

        var nonce = new byte[NonceSizeInBytes];
        var tag = new byte[TagSizeInBytes];
        var cipherLength = payload.Length - HeaderSizeInBytes;
        var cipherBytes = new byte[cipherLength];
        var plainBytes = new byte[cipherLength];

        Buffer.BlockCopy(payload, 1, nonce, 0, NonceSizeInBytes);
        Buffer.BlockCopy(payload, 1 + NonceSizeInBytes, tag, 0, TagSizeInBytes);
        Buffer.BlockCopy(payload, HeaderSizeInBytes, cipherBytes, 0, cipherLength);

        using (var aesGcm = new AesGcm(keyBytes, TagSizeInBytes))
        {
            aesGcm.Decrypt(nonce, cipherBytes, tag, plainBytes);
        }

        return Encoding.UTF8.GetString(plainBytes);
    }

    public string ComputeBlindIndex(string plainText)
    {
        if (string.IsNullOrWhiteSpace(plainText))
        {
            return string.Empty;
        }

        var clean = plainText.Trim().ToLowerInvariant();
        var dataBytes = Encoding.UTF8.GetBytes(clean);

        using var hmac = new HMACSHA256(_blindIndexKeyBytes);
        var hashBytes = hmac.ComputeHash(dataBytes);

        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
