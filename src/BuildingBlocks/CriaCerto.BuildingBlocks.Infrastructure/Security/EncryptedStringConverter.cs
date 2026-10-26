using CriaCerto.BuildingBlocks.Abstractions.Security;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CriaCerto.BuildingBlocks.Infrastructure.Security;

/// <summary>
/// EF Core ValueConverter transparente para cifragem de colunas de texto em repouso
/// via AES-256-GCM na escrita e decifragem automática na leitura (LGPD Art. 46).
/// </summary>
public sealed class EncryptedStringConverter : ValueConverter<string, string>
{
    public EncryptedStringConverter(IDataEncryptionService encryptionService)
        : base(
            v => encryptionService.Encrypt(v),
            v => encryptionService.Decrypt(v))
    {
    }
}
