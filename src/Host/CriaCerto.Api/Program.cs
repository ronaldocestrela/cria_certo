using System.Text;
using System.Threading.RateLimiting;
using CriaCerto.Api.BackgroundServices;
using CriaCerto.Api.Configuration;
using CriaCerto.Api.Endpoints;
using CriaCerto.Api.Endpoints.Backoffice;
using CriaCerto.Api.Middleware;
using CriaCerto.Api.Services;
using CriaCerto.BuildingBlocks.Application;
using CriaCerto.BuildingBlocks.Infrastructure;
using CriaCerto.Modules.Analytics.Application.Contracts;
using CriaCerto.Modules.Analytics.Application.Services;
using CriaCerto.Modules.Backoffice.Application;
using CriaCerto.Modules.Backoffice.Infrastructure;
using CriaCerto.Modules.Breeding.Application;
using CriaCerto.Modules.Breeding.Infrastructure;
using CriaCerto.Modules.Breeding.Infrastructure.Persistence;
using CriaCerto.Modules.Calving.Application.Contracts;
using CriaCerto.Modules.Calving.Infrastructure;
using CriaCerto.Modules.Calving.Infrastructure.Persistence;
using CriaCerto.Modules.Growth.Application.Contracts;
using CriaCerto.Modules.Growth.Infrastructure;
using CriaCerto.Modules.Growth.Infrastructure.Persistence;
using CriaCerto.Modules.Nutrition.Application;
using CriaCerto.Modules.Nutrition.Infrastructure;
using CriaCerto.Modules.Nutrition.Infrastructure.Persistence;
using CriaCerto.Modules.Referral.Application;
using CriaCerto.Modules.Referral.Application.Abstractions;
using CriaCerto.Modules.Referral.Application.Options;
using CriaCerto.Modules.Referral.Infrastructure;
using CriaCerto.Modules.Sanitary.Application.Contracts;
using CriaCerto.Modules.Sanitary.Infrastructure;
using CriaCerto.Modules.Sanitary.Infrastructure.Persistence;
using CriaCerto.Modules.Tenancy.Application;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Serilog;

DotEnvLoader.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "CriaCerto.Api")
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");
});

// Configure Data Protection to persist keys across container restarts
var keysDirectory = new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "dataprotection-keys"));
if (!keysDirectory.Exists)
{
    keysDirectory.Create();
}

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(keysDirectory)
    .SetApplicationName("CriaCertoApi");

builder.Services.AddOpenApi();

// Register Assemblies for MediatR and Validation discovery
builder.Services.AddBuildingBlocksApplication(
    typeof(Program).Assembly,
    typeof(BreedingAssemblyMarker).Assembly,
    typeof(CalvingDto).Assembly,
    typeof(PaddockDto).Assembly,
    typeof(NutritionAssemblyMarker).Assembly,
    typeof(VaccinationCampaignDto).Assembly,
    typeof(ExecutiveScorecardDto).Assembly,
    typeof(TenancyAssemblyMarker).Assembly,
    typeof(BackofficeAssemblyMarker).Assembly,
    typeof(ReferralAssemblyMarker).Assembly);

var connectionString = builder.Configuration.GetConnectionString("SqlServer")
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=localhost,1433;Database=criacerto_foundation;User Id=sa;Password=Password123!;TrustServerCertificate=True;Encrypt=False";

// Register Building Blocks and Infrastructure
builder.Services.AddBuildingBlocksInfrastructure(connectionString);
builder.Services.AddPiiRedaction();
builder.Services.AddDataEncryption(builder.Configuration);
builder.Services.AddEmailSender(builder.Configuration);
builder.Services.AddTenancyInfrastructure(builder.Configuration);
builder.Services.AddBreedingInfrastructure();
builder.Services.AddCalvingInfrastructure();
builder.Services.AddGrowthInfrastructure();
builder.Services.AddNutritionInfrastructure();
builder.Services.AddSanitaryModule(builder.Configuration);
builder.Services.AddBackofficeInfrastructure(builder.Configuration);
builder.Services.AddReferralInfrastructure(builder.Configuration);
builder.Services.AddScoped<IPartnerAccountProvisioner, PartnerAccountProvisioner>();
builder.Services.AddScoped<IPlanCatalogLookupService, PlanCatalogLookupService>();
builder.Services.AddScoped<ITenantInfoLookupService, TenantInfoLookupService>();
builder.Services.AddScoped<CriaCerto.Modules.Tenancy.Application.Abstractions.ITenantActiveHerdCounter, CompositeActiveHerdCounter>();
builder.Services.AddScoped<CriaCerto.Modules.Tenancy.Application.Abstractions.IPartnerRoleLookupService, PartnerRoleLookupService>();
builder.Services.AddScoped<IReferralPaymentVerificationService, StripePaymentVerificationService>();
builder.Services.AddScoped<IReferralAuditSink, ReferralBackofficeAuditSink>();
builder.Services.AddScoped<CriaCerto.Api.Services.TenancyBackofficeAuditSink>();
builder.Services.AddScoped<CriaCerto.Modules.Tenancy.Application.Abstractions.ITenancyAuditSink>(sp => sp.GetRequiredService<CriaCerto.Api.Services.TenancyBackofficeAuditSink>());
builder.Services.AddScoped<CriaCerto.Modules.Tenancy.Application.Abstractions.IUserAuditTrailLookupService>(sp => sp.GetRequiredService<CriaCerto.Api.Services.TenancyBackofficeAuditSink>());
builder.Services.AddScoped<CriaCerto.Modules.Tenancy.Application.Abstractions.IUserClientIpResolver, CriaCerto.Api.Services.HttpContextUserClientIpResolver>();
builder.Services.AddScoped<IPaidMediaSpendLookupService, PaidMediaSpendLookupService>();
builder.Services.Configure<ReferralEnforcementOptions>(builder.Configuration.GetSection(ReferralEnforcementOptions.SectionName));

// Background Services
builder.Services.AddHostedService<SubscriptionLifecycleWorker>();
builder.Services.AddHostedService<ReferralMaturityAndEnforcementWorker>();
builder.Services.Configure<DataRetentionPurgingOptions>(builder.Configuration.GetSection(DataRetentionPurgingOptions.SectionName));
builder.Services.AddSingleton<DataRetentionPurgingBackgroundService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<DataRetentionPurgingBackgroundService>());

// Configure CORS Policy
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
    ?? new[] { 
        "http://localhost:8081", 
        "http://localhost:8080", 
        "http://localhost:5000", 
        "http://localhost:5001", 
        "https://localhost:7001", 
        "http://localhost:5173",
        "https://criacerto.com.br"
    };

builder.Services.AddCors(options =>
{
    options.AddPolicy("ProductionCorsPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Configure JWT Authentication
var jwtSecret = builder.Configuration["Jwt:SecretKey"] 
    ?? builder.Configuration["JwtSettings:Secret"] 
    ?? Environment.GetEnvironmentVariable("JWT_SECRET") 
    ?? Environment.GetEnvironmentVariable("JWT_SECRET_KEY") 
    ?? "CriaCertoSuperSecretKeyThatIsAtLeast32BytesLong!";

if (builder.Environment.IsProduction())
{
    var envSecret = Environment.GetEnvironmentVariable("JWT_SECRET") 
        ?? Environment.GetEnvironmentVariable("JWT_SECRET_KEY") 
        ?? builder.Configuration["Jwt:SecretKey"] 
        ?? builder.Configuration["JwtSettings:Secret"];

    if (string.IsNullOrWhiteSpace(envSecret) || 
        envSecret.Contains("SuperSecretKey") || 
        Encoding.UTF8.GetByteCount(envSecret) < 32)
    {
        throw new InvalidOperationException("ERRO DE SEGURANÇA: Chave JWT de produção ausente, insegura ou inferior a 32 bytes.");
    }
    jwtSecret = envSecret;
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "CriaCerto",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "CriaCertoClient",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole(UserRole.Admin.ToString()));
    options.AddPolicy("ZootecniaOrAdmin", policy => policy.RequireRole(UserRole.Admin.ToString(), UserRole.Zootecnista.ToString()));
    options.AddPolicy("CurralAccess", policy => policy.RequireRole(UserRole.Admin.ToString(), UserRole.Zootecnista.ToString(), UserRole.Veterinario.ToString(), UserRole.OperadorCurral.ToString()));
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            code = "Backoffice.RateLimitExceeded",
            message = "Limite de requisições excedido. Por favor, aguarde alguns instantes antes de tentar novamente.",
            type = "Failure"
        }, cancellationToken: token);
    };

    options.AddPolicy("BackofficeAuthRateLimiter", httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ip,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 15,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });
});

var app = builder.Build();

if (app.Services.GetService<CriaCerto.BuildingBlocks.Abstractions.Tenancy.ITenantDatabaseProvisioner>() is CriaCerto.BuildingBlocks.Infrastructure.Tenancy.TenantDatabaseProvisioner provisioner)
{
    provisioner.RegisterTenantDbContextType(typeof(BreedingDbContext), "breeding");
    provisioner.RegisterTenantDbContextType(typeof(CalvingDbContext), "calving");
    provisioner.RegisterTenantDbContextType(typeof(GrowthDbContext), "growth");
    provisioner.RegisterTenantDbContextType(typeof(NutritionDbContext), "nutrition");
    provisioner.RegisterTenantDbContextType(typeof(SanitaryDbContext), "sanitary");
}

app.ApplyDatabaseMigrations();
app.SeedReferenceDatabase();

app.UseGlobalExceptionHandler();
app.UseSecurityHeaders();
app.UseRequestLogging();
app.UseCors("ProductionCorsPolicy");
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseTenantAccess();
app.UseTenantDatabase();
app.UseBackofficeModule();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "CriaCerto.Api" }))
    .WithName("Health");

app.MapBackofficeEndpoints();
app.MapAuthEndpoints();
app.MapTenancyEndpoints();
app.MapReferralEndpoints();
app.MapBreedingEndpoints();
app.MapCalvingEndpoints();
app.MapGrowthEndpoints();
app.MapNutritionEndpoints();
app.MapSanitaryEndpoints();
app.MapAnalyticsEndpoints();
app.MapReferenceEndpoints();
app.MapOfflineSyncEndpoints();

app.Run();

public partial class Program { }
