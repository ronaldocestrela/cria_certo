using CriaCerto.Shared.UI.Abstractions;
using Microsoft.JSInterop;

namespace CriaCerto.Web.Client.Services.Browser;

/// <summary>
/// Implementação browser de IFileExportService via JS Interop invocando downloadFileFromByteArray.
/// </summary>
public class BrowserFileExportService : IFileExportService
{
    private readonly IJSRuntime _jsRuntime;

    public BrowserFileExportService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task ExportFileAsync(string fileName, string contentType, byte[] content)
    {
        if (!OperatingSystem.IsBrowser())
        {
            return;
        }

        try
        {
            await _jsRuntime.InvokeVoidAsync("downloadFileFromByteArray", fileName, contentType, content);
        }
        catch (JSException)
        {
            // Ignorado em ambientes sem suporte a download direto
        }
        catch (InvalidOperationException)
        {
            // Ignorado durante prerender
        }
    }
}

