using CriaCerto.Web.Client.Auth;
using CriaCerto.Web.Client.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<CustomAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<CustomAuthStateProvider>());
builder.Services.AddScoped<PlantelApiClient>();
builder.Services.AddScoped<BreedingOpsApiClient>();
builder.Services.AddScoped<GrowthApiClient>();
builder.Services.AddScoped<NutritionApiClient>();
builder.Services.AddScoped<CalvingApiClient>();
builder.Services.AddScoped<TenancyApiClient>();
builder.Services.AddScoped<IOfflineSyncService, OfflineSyncService>();
builder.Services.AddScoped<IToastService, ToastService>();

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

await builder.Build().RunAsync();
