using CriaCerto.Mobile.Services;
using CriaCerto.Shared.UI.Abstractions;
using CriaCerto.Shared.UI.Services;
using Microsoft.Extensions.Logging;

namespace CriaCerto.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        // Registra serviços compartilhados de UI e ApiClients da RCL
        builder.Services.AddCriaCertoSharedServices();

        // Implementações iniciais de plataforma (serão aprofundadas com APIs nativas na Fase 4)
        builder.Services.AddScoped<ITokenStorageService, MobileTokenStorageStub>();
        builder.Services.AddScoped<IFileExportService, MobileFileExportStub>();
        builder.Services.AddScoped<IConnectivityService, MobileConnectivityStub>();
        builder.Services.AddScoped<IOfflineSyncStorage, MobileOfflineSyncStorageStub>();

        // Configuração de HttpClient apontando diretamente para a CriaCerto.Api
        builder.Services.AddScoped(sp =>
        {
            var apiBaseUri = GetApiBaseUri();
            return new HttpClient { BaseAddress = new Uri(apiBaseUri) };
        });

        return builder.Build();
    }

    private static string GetApiBaseUri()
    {
#if DEBUG
        if (DeviceInfo.Current.Platform == DevicePlatform.Android)
        {
            // Emulador Android redireciona 10.0.2.2 para o localhost da máquina host
            return "http://10.0.2.2:5274/";
        }
        return "http://localhost:5274/";
#else
        return "https://app.criacerto.com.br/";
#endif
    }
}

