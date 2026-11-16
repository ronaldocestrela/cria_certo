using CriaCerto.Shared.UI.Abstractions;
using Microsoft.Extensions.Logging;

namespace CriaCerto.Mobile.Services;

/// <summary>
/// Provedor nativo de exportação de arquivos para .NET MAUI.
/// Grava arquivos temporários no CacheDirectory e abre a folha nativa de compartilhamento (Share API).
/// </summary>
public class MauiFileExportService : IFileExportService
{
    private readonly ILogger<MauiFileExportService>? _logger;

    public MauiFileExportService(ILogger<MauiFileExportService>? logger = null)
    {
        _logger = logger;
    }

    public async Task ExportFileAsync(string fileName, string contentType, byte[] content)
    {
        try
        {
            var cacheDir = FileSystem.CacheDirectory;
            var filePath = Path.Combine(cacheDir, fileName);

            await File.WriteAllBytesAsync(filePath, content);

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = fileName,
                File = new ShareFile(filePath, contentType)
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Falha ao exportar ou compartilhar arquivo '{FileName}'.", fileName);
        }
    }
}

