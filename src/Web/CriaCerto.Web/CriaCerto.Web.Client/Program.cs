using CriaCerto.Shared.UI.Abstractions;
using CriaCerto.Shared.UI.Auth;
using CriaCerto.Shared.UI.Services;
using CriaCerto.Web.Client.Services.Browser;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();

// Abstrações de Plataforma (Browser Implementations)
builder.Services.AddScoped<ITokenStorageService, BrowserTokenStorageService>();
builder.Services.AddScoped<IFileExportService, BrowserFileExportService>();
builder.Services.AddScoped<IConnectivityService, BrowserConnectivityService>();
builder.Services.AddScoped<IOfflineSyncStorage, BrowserOfflineSyncStorage>();

// Serviços de Autenticação, Clientes de API e Estado de UI Compartilhados (RCL)
builder.Services.AddCriaCertoSharedServices();

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

await builder.Build().RunAsync();

namespace CriaCerto.Web.Client
{
    public partial class Program { }
}
