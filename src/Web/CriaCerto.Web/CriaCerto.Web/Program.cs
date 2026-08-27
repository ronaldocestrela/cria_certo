using CriaCerto.Web.Components;
using CriaCerto.Web.Client.Services;
using CriaCerto.Web.Client.Auth;
using Microsoft.AspNetCore.DataProtection;
using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Configure Data Protection to persist keys across container restarts
var keysDirectory = new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "dataprotection-keys"));
if (!keysDirectory.Exists)
{
    keysDirectory.Create();
}

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(keysDirectory)
    .SetApplicationName("CriaCertoWeb");

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<CustomAuthStateProvider>();
builder.Services.AddScoped<Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider>(sp => 
    sp.GetRequiredService<CustomAuthStateProvider>());

var apiBaseUrl = builder.Configuration["ApiBaseUrl"] 
    ?? builder.Configuration["API_BASE_URL"] 
    ?? "http://localhost:8080";

builder.Services.AddReverseProxy()
    .LoadFromMemory(
        new[]
        {
            new RouteConfig
            {
                RouteId = "api-route",
                ClusterId = "api-cluster",
                Match = new RouteMatch
                {
                    Path = "/api/{**catch-all}"
                }
            }
        },
        new[]
        {
            new ClusterConfig
            {
                ClusterId = "api-cluster",
                Destinations = new Dictionary<string, DestinationConfig>
                {
                    { "api-destination", new DestinationConfig { Address = apiBaseUrl } }
                }
            }
        });

builder.Services.AddScoped(sp => new HttpClient());
builder.Services.AddScoped<PlantelApiClient>();
builder.Services.AddScoped<BreedingOpsApiClient>();
builder.Services.AddScoped<GrowthApiClient>();
builder.Services.AddScoped<NutritionApiClient>();
builder.Services.AddScoped<CalvingApiClient>();
builder.Services.AddScoped<TenancyApiClient>();
builder.Services.AddScoped<IBackofficeApiClient, BackofficeApiClient>();
builder.Services.AddScoped<IBackofficePermissionService, BackofficePermissionService>();
builder.Services.AddScoped<IImpersonationStateService, ImpersonationStateService>();
builder.Services.AddScoped<IOfflineSyncService, OfflineSyncService>();
builder.Services.AddScoped<IToastService, ToastService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
    context.Response.Headers.Append("Permissions-Policy", "unload=*");
    await next();
});

app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Microsoft.AspNetCore.Antiforgery.AntiforgeryValidationException)
    {
        foreach (var cookieKey in context.Request.Cookies.Keys)
        {
            if (cookieKey.StartsWith(".AspNetCore.Antiforgery", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.Cookies.Delete(cookieKey);
            }
        }

        if (!context.Response.HasStarted)
        {
            context.Response.Redirect(context.Request.Path);
        }
    }
});

app.UseAntiforgery();

app.MapReverseProxy();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(CriaCerto.Web.Client._Imports).Assembly)
    .AllowAnonymous();

app.Run();
