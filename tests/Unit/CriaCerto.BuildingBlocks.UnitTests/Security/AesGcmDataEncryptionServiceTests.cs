using System.Security.Cryptography;
using CriaCerto.BuildingBlocks.Abstractions.Security;
using CriaCerto.BuildingBlocks.Infrastructure.Security;
using FluentAssertions;
using Xunit;

namespace CriaCerto.BuildingBlocks.UnitTests.Security;

public class AesGcmDataEncryptionServiceTests
{
    private readonly AesGcmDataEncryptionService _service;
    private readonly EncryptionOptions _options;

    public AesGcmDataEncryptionServiceTests()
    {
        _options = new EncryptionOptions
        {
            CurrentKey = EncryptionDefaults.DefaultDevKey,
            CurrentKeyVersion = 1,
            BlindIndexKey = EncryptionDefaults.DefaultDevBlindIndexKey
        };
        _service = new AesGcmDataEncryptionService(_options);
    }

    [Fact]
    public void Encrypt_ShouldReturnCiphertextWithEnvelopePrefix()
    {
        // Arrange
        const string plainText = "123.456.789-00";

        // Act
        var cipher = _service.Encrypt(plainText);

        // Assert
        cipher.Should().NotBeNullOrWhiteSpace();
        cipher.Should().StartWith(EncryptionDefaults.EnvelopePrefix);
        cipher.Should().NotBe(plainText);
    }

    [Fact]
    public void Encrypt_ThenDecrypt_ShouldReturnOriginalPlainText()
    {
        // Arrange
        const string original = "minha-chave-pix-segura@empresa.com.br";

        // Act
        var cipher = _service.Encrypt(original);
        var decrypted = _service.Decrypt(cipher);

        // Assert
        decrypted.Should().Be(original);
    }

    [Fact]
    public void Encrypt_CalledTwiceForSameInput_ShouldProduceDifferentCiphertextsDueToRandomNonce()
    {
        // Arrange
        const string plainText = "01.234.567/0001-89";

        // Act
        var cipher1 = _service.Encrypt(plainText);
        var cipher2 = _service.Encrypt(plainText);

        // Assert
        cipher1.Should().NotBe(cipher2);
        _service.Decrypt(cipher1).Should().Be(plainText);
        _service.Decrypt(cipher2).Should().Be(plainText);
    }

    [Fact]
    public void Decrypt_WhenGivenPlainTextWithoutPrefix_ShouldReturnOriginalTextGracefully()
    {
        // Arrange
        const string legacyPlainText = "98765432100";

        // Act
        var result = _service.Decrypt(legacyPlainText);

        // Assert
        result.Should().Be(legacyPlainText);
    }

    [Fact]
    public void Decrypt_WhenPayloadIsTampered_ShouldThrowCryptographicException()
    {
        // Arrange
        const string plainText = "dado-confidencial";
        var cipher = _service.Encrypt(plainText);

        // Tamper with one base64 character in payload
        var payload = cipher.Substring(EncryptionDefaults.EnvelopePrefix.Length);
        var bytes = Convert.FromBase64String(payload);
        bytes[^1] ^= 0xFF; // Flip bits of last byte
        var tamperedCipher = EncryptionDefaults.EnvelopePrefix + Convert.ToBase64String(bytes);

        // Act
        var act = () => _service.Decrypt(tamperedCipher);

        // Assert
        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void KeyRotation_ShouldDecryptDataEncryptedWithOldKeyVersion()
    {
        // Arrange: Criptografa com v1
        const string secretData = "11.222.333/0001-44";
        var cipherV1 = _service.Encrypt(secretData);

        // Nova chave v2 gerada aleatoriamente
        var keyV2Bytes = RandomNumberGenerator.GetBytes(32);
        var keyV2Base64 = Convert.ToBase64String(keyV2Bytes);

        var rotationOptions = new EncryptionOptions
        {
            CurrentKey = keyV2Base64,
            CurrentKeyVersion = 2,
            BlindIndexKey = _options.BlindIndexKey,
            KeyRing = new Dictionary<byte, string>
            {
                { 1, _options.CurrentKey } // v1 mantida no chaveiro
            }
        };

        var serviceV2 = new AesGcmDataEncryptionService(rotationOptions);

        // Act & Assert
        // 1. Consegue decifrar dado gerado com v1 usando a nova instância
        var decryptedOld = serviceV2.Decrypt(cipherV1);
        decryptedOld.Should().Be(secretData);

        // 2. Novos dados encriptados usam a versão 2
        var cipherV2 = serviceV2.Encrypt(secretData);
        var decryptedNew = serviceV2.Decrypt(cipherV2);
        decryptedNew.Should().Be(secretData);
    }

    [Fact]
    public void ComputeBlindIndex_ShouldBeDeterministicAndProduce64HexChars()
    {
        // Arrange
        const string doc1 = "12345678900";
        const string doc2 = "12345678900";
        const string doc3 = "98765432100";

        // Act
        var hash1 = _service.ComputeBlindIndex(doc1);
        var hash2 = _service.ComputeBlindIndex(doc2);
        var hash3 = _service.ComputeBlindIndex(doc3);

        // Assert
        hash1.Should().NotBeNullOrWhiteSpace();
        hash1.Length.Should().Be(64); // SHA-256 Hex string
        hash1.Should().Be(hash2);
        hash1.Should().NotBe(hash3);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void EncryptAndDecrypt_EmptyOrNull_ShouldReturnOriginal(string? input)
    {
        _service.Encrypt(input!).Should().Be(input);
        _service.Decrypt(input!).Should().Be(input);
    }
}
