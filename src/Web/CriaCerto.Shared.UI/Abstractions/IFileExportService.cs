namespace CriaCerto.Shared.UI.Abstractions;

/// <summary>
/// Serviço de exportação e download de arquivos em memória (CSV, JSON, PDF).
/// Suporta download via browser (Blob / JS Interop) e compartilhamento nativo no mobile (.NET MAUI Share API).
/// </summary>
public interface IFileExportService
{
    Task ExportFileAsync(string fileName, string contentType, byte[] content);
}

