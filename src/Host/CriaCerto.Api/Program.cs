using System.Security.Claims;
using System.Text;
using CriaCerto.Api.Middleware;
using CriaCerto.BuildingBlocks.Application.Features.GetReferenceBreeds;
using CriaCerto.Api.Seeders;
using CriaCerto.Modules.Sanitary.Application.Features.GetVaccineCalendar;
using CriaCerto.BuildingBlocks.Application;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.BuildingBlocks.Infrastructure;
using CriaCerto.BuildingBlocks.Infrastructure.Persistence;
using CriaCerto.Modules.Breeding.Application.Domain;
using CriaCerto.Modules.Breeding.Application.Contracts;
using CriaCerto.Modules.Breeding.Application.Features.Plantel;
using CriaCerto.Modules.Breeding.Application.Features.BreedingOps;
using CriaCerto.Modules.Breeding.Infrastructure;
using CriaCerto.Modules.Breeding.Infrastructure.Persistence;
using CriaCerto.Modules.Calving.Application.Contracts;
using CriaCerto.Modules.Calving.Infrastructure;
using CriaCerto.Modules.Calving.Infrastructure.Persistence;
using CriaCerto.Modules.Growth.Application.Contracts;
using CriaCerto.Modules.Growth.Application.Features.DispatchFeatures;
using CriaCerto.Modules.Growth.Infrastructure;
using CriaCerto.Modules.Growth.Infrastructure.Persistence;
using CriaCerto.Modules.Nutrition.Application;
using CriaCerto.Modules.Nutrition.Application.Contracts;
using CriaCerto.Modules.Nutrition.Application.Features.AnalyticsFeatures;
using CriaCerto.Modules.Nutrition.Application.Features.FeedingFeatures;
using CriaCerto.Modules.Nutrition.Application.Features.RationFeatures;
using CriaCerto.Modules.Nutrition.Application.Features.SiloStockFeatures;
using CriaCerto.Modules.Nutrition.Infrastructure;
using CriaCerto.Modules.Nutrition.Infrastructure.Persistence;
using CriaCerto.Modules.Tenancy.Application;
using CriaCerto.Modules.Tenancy.Application.Features.Login;
using CriaCerto.Modules.Tenancy.Application.Features.RegisterUser;
using CriaCerto.Modules.Tenancy.Application.Features.CreateTenant;
using CriaCerto.Modules.Tenancy.Application.Features.ForgotPassword;
using CriaCerto.Modules.Tenancy.Application.Features.ResetPassword;
using CriaCerto.Modules.Tenancy.Application.Features.SelectTenant;
using CriaCerto.Modules.Tenancy.Application.Features.GetSubscriptionPlans;
using CriaCerto.Modules.Tenancy.Application.Features.GetTenantProfile;
using CriaCerto.Modules.Tenancy.Application.Features.UpdateTenantProfile;
using CriaCerto.Modules.Tenancy.Application.Features.ChangeSubscriptionPlan;
using CriaCerto.Modules.Tenancy.Application.Features.GetProductionUnits;
using CriaCerto.Modules.Tenancy.Application.Features.CreateProductionUnit;
using CriaCerto.Modules.Tenancy.Application.Features.UpdateProductionUnit;
using CriaCerto.Modules.Tenancy.Application.Features.InviteTeamMember;
using CriaCerto.Modules.Tenancy.Application.Features.GetTeamMembers;
using CriaCerto.Modules.Tenancy.Application.Features.AcceptTeamInvite;
using CriaCerto.Modules.Tenancy.Application.Features.RevokeTeamInvite;
using CriaCerto.Modules.Tenancy.Application.Features.RemoveTeamMember;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Infrastructure;

using CriaCerto.Modules.Tenancy.Infrastructure.Persistence;
using CriaCerto.Modules.Sanitary.Application.Contracts;
using CriaCerto.Modules.Sanitary.Infrastructure;
using CriaCerto.Modules.Sanitary.Infrastructure.Persistence;
using CriaCerto.Modules.Analytics.Application.Contracts;
using CriaCerto.Modules.Analytics.Application.Services;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Tokens;

using CriaCerto.Modules.Backoffice.Application;
using CriaCerto.Modules.Backoffice.Application.Features.Dashboard;
using CriaCerto.Modules.Backoffice.Application.Features.AdminUsers.Commands;
using CriaCerto.Modules.Backoffice.Application.Features.AdminUsers.Queries;
using CriaCerto.Modules.Backoffice.Application.Features.AdminUsers.Dtos;
using CriaCerto.Modules.Backoffice.Application.Features.Tenants.Commands;
using CriaCerto.Modules.Backoffice.Application.Features.Tenants.Dtos;
using CriaCerto.Modules.Backoffice.Application.Features.Tenants.Queries;
using CriaCerto.Modules.Backoffice.Application.Features.Plans.Commands;
using CriaCerto.Modules.Backoffice.Application.Features.Plans.Queries;
using CriaCerto.Modules.Backoffice.Application.Features.Plans.Dtos;
using CriaCerto.Modules.Backoffice.Application.Features.Impersonation.Commands;
using CriaCerto.Modules.Backoffice.Application.Features.Impersonation.Queries;
using CriaCerto.Modules.Backoffice.Application.Features.Impersonation.Dtos;
using CriaCerto.Modules.Backoffice.Application.Security;
using CriaCerto.Modules.Backoffice.Infrastructure;
using CriaCerto.Modules.Backoffice.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

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
    typeof(CriaCerto.Modules.Breeding.Application.BreedingAssemblyMarker).Assembly,
    typeof(CriaCerto.Modules.Calving.Application.Contracts.CalvingDto).Assembly,
    typeof(CriaCerto.Modules.Growth.Application.Contracts.PaddockDto).Assembly,
    typeof(NutritionAssemblyMarker).Assembly,
    typeof(VaccinationCampaignDto).Assembly,
    typeof(ExecutiveScorecardDto).Assembly,
    typeof(TenancyAssemblyMarker).Assembly,
    typeof(BackofficeAssemblyMarker).Assembly);

var connectionString = builder.Configuration.GetConnectionString("SqlServer")
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=localhost,1433;Database=criacerto_foundation;User Id=sa;Password=Password123!;TrustServerCertificate=True;Encrypt=False";

// Register Building Blocks and Infrastructure
builder.Services.AddBuildingBlocksInfrastructure(connectionString);
builder.Services.AddTenancyInfrastructure(builder.Configuration);
builder.Services.AddBreedingInfrastructure();
builder.Services.AddCalvingInfrastructure();
builder.Services.AddGrowthInfrastructure();
builder.Services.AddNutritionInfrastructure();
builder.Services.AddSanitaryModule(builder.Configuration);
builder.Services.AddBackofficeInfrastructure(builder.Configuration);

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

var app = builder.Build();

if (app.Services.GetService<CriaCerto.BuildingBlocks.Abstractions.Tenancy.ITenantDatabaseProvisioner>() is CriaCerto.BuildingBlocks.Infrastructure.Tenancy.TenantDatabaseProvisioner provisioner)
{
    provisioner.RegisterTenantDbContextType(typeof(BreedingDbContext), "breeding");
    provisioner.RegisterTenantDbContextType(typeof(CalvingDbContext), "calving");
    provisioner.RegisterTenantDbContextType(typeof(GrowthDbContext), "growth");
    provisioner.RegisterTenantDbContextType(typeof(NutritionDbContext), "nutrition");
    provisioner.RegisterTenantDbContextType(typeof(SanitaryDbContext), "sanitary");
}

ApplyMigrations(app);
SeedReferenceData(app);

app.UseSecurityHeaders();
app.UseCors("ProductionCorsPolicy");

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

// --- BACKOFFICE ADMIN ENDPOINTS ---
var backoffice = app.MapGroup("/api/v1/backoffice").RequireAuthorization();

backoffice.MapGet("/dashboard/kpis", async (ISender sender) =>
{
    var result = await sender.Send(new GetBackofficeDashboardKpisQuery());
    return ToHttpResult(result);
}).WithTags("Backoffice");

// Admin Users Management Endpoints
backoffice.MapGet("/users", async (
    string? searchTerm,
    bool? isActive,
    string? roleName,
    int? page,
    int? pageSize,
    ISender sender) =>
{
    var query = new GetAdminUsersQuery(searchTerm, isActive, roleName, page ?? 1, pageSize ?? 20);
    var result = await sender.Send(query);
    return ToHttpResult(result);
}).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.UsersAdminManage)).WithTags("Backoffice IAM");

backoffice.MapGet("/users/{id:guid}", async (Guid id, ISender sender) =>
{
    var result = await sender.Send(new GetAdminUserByIdQuery(id));
    return ToHttpResult(result);
}).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.UsersAdminManage)).WithTags("Backoffice IAM");

backoffice.MapPost("/users", async (CreateAdminUserRequest req, HttpContext ctx, ISender sender) =>
{
    var (callerId, callerEmail, ip) = GetBackofficeActor(ctx);
    var command = new CreateAdminUserCommand(req.Name, req.Email, req.RawPassword, req.RoleIds, callerId, callerEmail, ip);
    var result = await sender.Send(command);
    return ToHttpResult(result, StatusCodes.Status201Created);
}).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.UsersAdminManage)).WithTags("Backoffice IAM");

backoffice.MapPut("/users/{id:guid}", async (Guid id, UpdateAdminUserRequest req, HttpContext ctx, ISender sender) =>
{
    var (callerId, callerEmail, ip) = GetBackofficeActor(ctx);
    var command = new UpdateAdminUserCommand(id, req.Name, req.Email, req.RoleIds, callerId, callerEmail, ip);
    var result = await sender.Send(command);
    return ToHttpResult(result);
}).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.UsersAdminManage)).WithTags("Backoffice IAM");

backoffice.MapPatch("/users/{id:guid}/status", async (Guid id, ToggleStatusRequest req, HttpContext ctx, ISender sender) =>
{
    var (callerId, callerEmail, ip) = GetBackofficeActor(ctx);
    var command = new ToggleAdminUserStatusCommand(id, req.IsActive, callerId, callerEmail, ip);
    var result = await sender.Send(command);
    return result.IsSuccess ? Results.Ok() : Results.Json(result.Error, statusCode: ToStatusCode(result.Error.Type));
}).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.UsersAdminManage)).WithTags("Backoffice IAM");

backoffice.MapPost("/users/{id:guid}/reset-password", async (Guid id, ResetPasswordRequest req, HttpContext ctx, ISender sender) =>
{
    var (callerId, callerEmail, ip) = GetBackofficeActor(ctx);
    var command = new ResetAdminUserPasswordCommand(id, req.NewRawPassword, callerId, callerEmail, ip);
    var result = await sender.Send(command);
    return result.IsSuccess ? Results.Ok() : Results.Json(result.Error, statusCode: ToStatusCode(result.Error.Type));
}).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.UsersAdminManage)).WithTags("Backoffice IAM");

// MFA Endpoints
backoffice.MapPost("/users/{id:guid}/mfa/setup", async (Guid id, ISender sender) =>
{
    var result = await sender.Send(new GenerateMfaSetupCommand(id));
    return ToHttpResult(result);
}).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.UsersAdminManage)).WithTags("Backoffice MFA");

backoffice.MapPost("/users/{id:guid}/mfa/enable", async (Guid id, EnableMfaRequest req, HttpContext ctx, ISender sender) =>
{
    var (callerId, callerEmail, ip) = GetBackofficeActor(ctx);
    var command = new EnableMfaCommand(id, req.SecretKey, req.VerificationCode, req.RecoveryCodes, callerId, callerEmail, ip);
    var result = await sender.Send(command);
    return result.IsSuccess ? Results.Ok() : Results.Json(result.Error, statusCode: ToStatusCode(result.Error.Type));
}).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.UsersAdminManage)).WithTags("Backoffice MFA");

backoffice.MapPost("/users/{id:guid}/mfa/disable", async (Guid id, HttpContext ctx, ISender sender) =>
{
    var (callerId, callerEmail, ip) = GetBackofficeActor(ctx);
    var command = new DisableMfaCommand(id, callerId, callerEmail, ip);
    var result = await sender.Send(command);
    return result.IsSuccess ? Results.Ok() : Results.Json(result.Error, statusCode: ToStatusCode(result.Error.Type));
}).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.UsersAdminManage)).WithTags("Backoffice MFA");

// Session Management Endpoints
backoffice.MapDelete("/sessions/{sessionId:guid}", async (Guid sessionId, HttpContext ctx, ISender sender) =>
{
    var (callerId, callerEmail, ip) = GetBackofficeActor(ctx);
    var command = new RevokeAdminSessionCommand(sessionId, callerId, callerEmail, ip);
    var result = await sender.Send(command);
    return result.IsSuccess ? Results.Ok() : Results.Json(result.Error, statusCode: ToStatusCode(result.Error.Type));
}).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.UsersAdminManage)).WithTags("Backoffice Sessions");

backoffice.MapDelete("/users/{id:guid}/sessions", async (Guid id, HttpContext ctx, ISender sender) =>
{
    var (callerId, callerEmail, ip) = GetBackofficeActor(ctx);
    var command = new RevokeAllUserSessionsCommand(id, callerId, callerEmail, ip);
    var result = await sender.Send(command);
    return result.IsSuccess ? Results.Ok() : Results.Json(result.Error, statusCode: ToStatusCode(result.Error.Type));
}).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.UsersAdminManage)).WithTags("Backoffice Sessions");

// Tenant Management Endpoints
backoffice.MapGet("/tenants", async (
    string? searchTerm,
    string? status,
    string? subscribedPlan,
    string? state,
    string? ownerSearch,
    string? sizeSegment,
    string? commercialRegion,
    string? productiveProfile,
    string? churnRisk,
    Guid[]? tagIds,
    bool? includeInactiveTags,
    DateTime? afterCreatedAtUtc,
    Guid? afterId,
    int? page,
    int? pageSize,
    ISender sender) =>
{
    var query = new GetTenantsAdminQuery(
        searchTerm,
        status,
        subscribedPlan,
        state,
        ownerSearch,
        sizeSegment,
        commercialRegion,
        productiveProfile,
        churnRisk,
        tagIds,
        includeInactiveTags ?? false,
        afterCreatedAtUtc,
        afterId,
        page ?? 1,
        pageSize ?? 20);
    var result = await sender.Send(query);
    return ToHttpResult(result);
}).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.TenantsRead)).WithTags("Backoffice Tenants");

backoffice.MapGet("/tenants/export", async (
    string? searchTerm,
    string? status,
    string? subscribedPlan,
    string? state,
    string? ownerSearch,
    string? sizeSegment,
    string? commercialRegion,
    string? productiveProfile,
    string? churnRisk,
    Guid[]? tagIds,
    bool? includeInactiveTags,
    HttpContext ctx,
    ISender sender) =>
{
    var (callerId, callerEmail, ip) = GetBackofficeActor(ctx);
    var exportQuery = new ExportTenantsAdminQuery(
        searchTerm,
        status,
        subscribedPlan,
        state,
        ownerSearch,
        sizeSegment,
        commercialRegion,
        productiveProfile,
        churnRisk,
        tagIds,
        includeInactiveTags ?? false);

    var result = await sender.Send(exportQuery);
    if (result.IsFailure)
    {
        return Results.Json(result.Error, statusCode: ToStatusCode(result.Error.Type));
    }

    await sender.Send(new ExportTenantsAdminAuditCommand(
        exportQuery, callerId, callerEmail, ip, result.Value));

    return Results.File(result.Value.Content, "text/csv", result.Value.FileName);
}).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.TenantsRead)).WithTags("Backoffice Tenants");

backoffice.MapGet("/tenants/{id:guid}", async (Guid id, ISender sender) =>
{
    var result = await sender.Send(new GetTenantAdminDetailQuery(id));
    return ToHttpResult(result);
}).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.TenantsRead)).WithTags("Backoffice Tenants");

backoffice.MapPost("/tenants", async (CreateTenantAdminRequest req, HttpContext ctx, ISender sender) =>
{
    var (callerId, callerEmail, ip) = GetBackofficeActor(ctx);
    var command = new CreateTenantAdminCommand(
        req.Name, req.LegalName, req.CNPJ, req.ExternalIdentifier,
        req.State, req.City, req.StateRegistration, req.AreaInHectares,
        req.SubscribedPlan, req.Capacity, req.Type,
        req.TechnicalOwnerName, req.TechnicalOwnerEmail,
        req.CommercialOwnerName, req.CommercialOwnerEmail, req.OwnerUserEmail,
        callerId, callerEmail, ip);
    var result = await sender.Send(command);
    return ToHttpResult(result, StatusCodes.Status201Created);
}).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.TenantsWrite)).WithTags("Backoffice Tenants");

backoffice.MapPut("/tenants/{id:guid}", async (Guid id, UpdateTenantAdminRequest req, HttpContext ctx, ISender sender) =>
{
    var (callerId, callerEmail, ip) = GetBackofficeActor(ctx);
    var command = new UpdateTenantAdminCommand(
        id, req.Name, req.LegalName, req.CNPJ, req.ExternalIdentifier,
        req.State, req.City, req.StateRegistration, req.AreaInHectares,
        req.Capacity, req.Type,
        req.TechnicalOwnerName, req.TechnicalOwnerEmail,
        req.CommercialOwnerName, req.CommercialOwnerEmail,
        callerId, callerEmail, ip);
    var result = await sender.Send(command);
    return ToHttpResult(result);
}).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.TenantsWrite)).WithTags("Backoffice Tenants");

backoffice.MapPost("/tenants/{id:guid}/suspend", async (Guid id, TenantLifecycleActionRequest req, HttpContext ctx, ISender sender) =>
{
    var (callerId, callerEmail, ip) = GetBackofficeActor(ctx);
    var command = new SuspendTenantAdminCommand(id, req.Reason, callerId, callerEmail, ip);
    var result = await sender.Send(command);
    return ToHttpResult(result);
}).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.TenantsSuspend)).WithTags("Backoffice Tenants");

backoffice.MapPost("/tenants/{id:guid}/reactivate", async (Guid id, TenantLifecycleActionRequest req, HttpContext ctx, ISender sender) =>
{
    var (callerId, callerEmail, ip) = GetBackofficeActor(ctx);
    var command = new ReactivateTenantAdminCommand(id, req.Reason, callerId, callerEmail, ip);
    var result = await sender.Send(command);
    return ToHttpResult(result);
}).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.TenantsSuspend)).WithTags("Backoffice Tenants");

backoffice.MapPost("/tenants/{id:guid}/cancel", async (Guid id, TenantLifecycleActionRequest req, HttpContext ctx, ISender sender) =>
{
    var (callerId, callerEmail, ip) = GetBackofficeActor(ctx);
    var command = new CancelTenantAdminCommand(id, req.Reason, callerId, callerEmail, ip);
    var result = await sender.Send(command);
    return ToHttpResult(result);
}).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.TenantsSuspend)).WithTags("Backoffice Tenants");

backoffice.MapPost("/tenants/{id:guid}/archive", async (Guid id, TenantLifecycleActionRequest req, HttpContext ctx, ISender sender) =>
{
    var (callerId, callerEmail, ip) = GetBackofficeActor(ctx);
    var command = new ArchiveTenantAdminCommand(id, req.Reason, callerId, callerEmail, ip);
    var result = await sender.Send(command);
    return ToHttpResult(result);
}).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.TenantsSuspend)).WithTags("Backoffice Tenants");

backoffice.MapPost("/tenants/{id:guid}/protection", async (Guid id, TenantProtectionRequest req, HttpContext ctx, ISender sender) =>
{
    var (callerId, callerEmail, ip) = GetBackofficeActor(ctx);
    var command = new SetTenantProtectionAdminCommand(id, req.IsProtected, req.Reason, callerId, callerEmail, ip);
    var result = await sender.Send(command);
    return ToHttpResult(result);
}).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.TenantsSuspend)).WithTags("Backoffice Tenants");

backoffice.MapPut("/tenants/{id:guid}/segmentation", async (Guid id, UpdateTenantSegmentationAdminRequest req, HttpContext ctx, ISender sender) =>
{
    var (callerId, callerEmail, ip) = GetBackofficeActor(ctx);
    var command = new UpdateTenantSegmentationAdminCommand(
        id, req.SizeSegment, req.CommercialRegion, req.ProductiveProfile, req.ChurnRisk,
        callerId, callerEmail, ip);
    var result = await sender.Send(command);
    return ToHttpResult(result);
}).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.TenantsWrite)).WithTags("Backoffice Tenants");

backoffice.MapPut("/tenants/{id:guid}/tags", async (Guid id, ReplaceTenantTagsAdminRequest req, HttpContext ctx, ISender sender) =>
{
    var (callerId, callerEmail, ip) = GetBackofficeActor(ctx);
    var command = new ReplaceTenantTagsAdminCommand(id, req.TagIds, callerId, callerEmail, ip);
    var result = await sender.Send(command);
    return ToHttpResult(result);
}).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.TenantsWrite)).WithTags("Backoffice Tenants");

backoffice.MapGet("/tenants/tags", async (bool? includeInactive, ISender sender) =>
{
    var result = await sender.Send(new GetOperationalTagsAdminQuery(includeInactive ?? false));
    return ToHttpResult(result);
}).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.TenantsRead)).WithTags("Backoffice Tenants");

backoffice.MapPost("/tenants/tags", async (CreateOperationalTagAdminRequest req, HttpContext ctx, ISender sender) =>
{
    var (callerId, callerEmail, ip) = GetBackofficeActor(ctx);
    var command = new CreateOperationalTagAdminCommand(req.Name, req.Category, req.ColorHex, callerId, callerEmail, ip);
    var result = await sender.Send(command);
    return ToHttpResult(result, StatusCodes.Status201Created);
}).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.TenantsWrite)).WithTags("Backoffice Tenants");

backoffice.MapDelete("/tenants/tags/{tagId:guid}", async (Guid tagId, HttpContext ctx, ISender sender) =>
{
    var (callerId, callerEmail, ip) = GetBackofficeActor(ctx);
    var command = new DeactivateOperationalTagAdminCommand(tagId, callerId, callerEmail, ip);
    var result = await sender.Send(command);
    return ToHttpResult(result);
}).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.TenantsWrite)).WithTags("Backoffice Tenants");

backoffice.MapGet("/tenants/saved-filters", async (HttpContext ctx, ISender sender) =>
{
    var (callerId, _, _) = GetBackofficeActor(ctx);
    var result = await sender.Send(new GetAdminSavedFiltersQuery(callerId));
    return ToHttpResult(result);
}).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.TenantsRead)).WithTags("Backoffice Tenants");

backoffice.MapPost("/tenants/saved-filters", async (SaveAdminFilterRequest req, HttpContext ctx, ISender sender) =>
{
    var (callerId, _, _) = GetBackofficeActor(ctx);
    var command = new SaveAdminFilterCommand(callerId, req.Name, req.Filter, req.IsDefault);
    var result = await sender.Send(command);
    return ToHttpResult(result, StatusCodes.Status201Created);
}).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.TenantsRead)).WithTags("Backoffice Tenants");

backoffice.MapDelete("/tenants/saved-filters/{filterId:guid}", async (Guid filterId, HttpContext ctx, ISender sender) =>
{
    var (callerId, _, _) = GetBackofficeActor(ctx);
    var result = await sender.Send(new DeleteAdminFilterCommand(callerId, filterId));
    return result.IsSuccess ? Results.Ok() : Results.Json(result.Error, statusCode: ToStatusCode(result.Error.Type));
}).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.TenantsRead)).WithTags("Backoffice Tenants");

backoffice.MapGet("/tenants/{id:guid}/plan-preview", async (Guid id, Guid targetPlanVersionId, ISender sender) =>
{
    var query = new PreviewTenantPlanChangeQuery(id, targetPlanVersionId);
    var result = await sender.Send(query);
    return ToHttpResult(result);
}).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.TenantsRead)).WithTags("Backoffice Subscriptions");

backoffice.MapPost("/tenants/{id:guid}/plan", async (Guid id, ChangeTenantPlanRequestDto req, HttpContext ctx, ISender sender) =>
{
    var (callerId, _, _) = GetBackofficeActor(ctx);
    var command = new ChangeTenantPlanCommand(id, req.TargetPlanVersionId, callerId, req.Justification, req.ForceImmediate);
    var result = await sender.Send(command);
    return ToHttpResult(result);
}).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.TenantsWrite)).WithTags("Backoffice Subscriptions");

// --- BACKOFFICE PLAN CATALOG ENDPOINTS ---
backoffice.MapGet("/plans", async (bool? includeArchived, ISender sender) =>
{
    var result = await sender.Send(new GetPlanCatalogsQuery(includeArchived ?? false));
    return ToHttpResult(result);
}).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.PlansRead)).WithTags("Backoffice Plans");

backoffice.MapGet("/plans/{id:guid}", async (Guid id, ISender sender) =>
{
    var result = await sender.Send(new GetPlanCatalogByIdQuery(id));
    return ToHttpResult(result);
}).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.PlansRead)).WithTags("Backoffice Plans");

backoffice.MapGet("/plans/versions/compare", async (Guid baseVersionId, Guid targetVersionId, ISender sender) =>
{
    var result = await sender.Send(new ComparePlanVersionsQuery(baseVersionId, targetVersionId));
    return ToHttpResult(result);
}).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.PlansRead)).WithTags("Backoffice Plans");

backoffice.MapPost("/plans", async (CreatePlanCatalogCommand req, HttpContext ctx, ISender sender) =>
{
    var (callerId, callerEmail, ip) = GetBackofficeActor(ctx);
    var command = req with { PerformedByAdminUserId = callerId, PerformedByAdminEmail = callerEmail, IpAddress = ip };
    var result = await sender.Send(command);
    return ToHttpResult(result, StatusCodes.Status201Created);
}).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.PlansWrite)).WithTags("Backoffice Plans");

backoffice.MapPost("/plans/{id:guid}/versions", async (Guid id, CreatePlanVersionCommand req, HttpContext ctx, ISender sender) =>
{
    var (callerId, callerEmail, ip) = GetBackofficeActor(ctx);
    var command = req with { PlanCatalogId = id, PerformedByAdminUserId = callerId, PerformedByAdminEmail = callerEmail, IpAddress = ip };
    var result = await sender.Send(command);
    return ToHttpResult(result, StatusCodes.Status201Created);
}).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.PlansWrite)).WithTags("Backoffice Plans");

backoffice.MapPut("/plans/versions/{versionId:guid}", async (Guid versionId, UpdateDraftPlanVersionCommand req, HttpContext ctx, ISender sender) =>
{
    var (callerId, callerEmail, ip) = GetBackofficeActor(ctx);
    var command = req with { VersionId = versionId, PerformedByAdminUserId = callerId, PerformedByAdminEmail = callerEmail, IpAddress = ip };
    var result = await sender.Send(command);
    return ToHttpResult(result);
}).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.PlansWrite)).WithTags("Backoffice Plans");

backoffice.MapPost("/plans/versions/{versionId:guid}/publish", async (Guid versionId, PublishPlanVersionRequest req, HttpContext ctx, ISender sender) =>
{
    var (callerId, callerEmail, ip) = GetBackofficeActor(ctx);
    var command = new PublishPlanVersionCommand(versionId, req.ApprovalNotes, callerId, callerEmail, ip);
    var result = await sender.Send(command);
    return ToHttpResult(result);
}).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.PlansPublish)).WithTags("Backoffice Plans");

// --- BACKOFFICE IMPERSONATION ENDPOINTS ---
backoffice.MapPost("/impersonation/start", async (StartImpersonationRequest req, HttpContext ctx, ISender sender) =>
{
    var (callerId, callerEmail, ip) = GetBackofficeActor(ctx);
    var ua = ctx.Request.Headers.UserAgent.ToString() ?? "Unknown";
    var command = new StartImpersonationSessionCommand(
        req.TargetTenantId,
        req.TargetUserId,
        req.SupportTicket,
        req.Justification,
        req.DurationMinutes,
        callerId,
        callerEmail,
        ip,
        ua);
    var result = await sender.Send(command);
    return ToHttpResult(result, StatusCodes.Status201Created);
}).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.ImpersonationStart)).WithTags("Backoffice Impersonation");

backoffice.MapPost("/impersonation/stop", async (StopImpersonationRequest req, HttpContext ctx, ISender sender) =>
{
    var (callerId, callerEmail, ip) = GetBackofficeActor(ctx);
    var isPlatformOwner = ctx.User.HasClaim("is_platform_owner", "true") || ctx.User.IsInRole(BackofficeRoles.PlatformOwner);
    var command = new StopImpersonationSessionCommand(req.SessionId, callerId, callerEmail, ip, isPlatformOwner);
    var result = await sender.Send(command);
    return ToCommandHttpResult(result);
}).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.ImpersonationStop)).WithTags("Backoffice Impersonation");

backoffice.MapGet("/impersonation/active", async (HttpContext ctx, ISender sender) =>
{
    var (callerId, _, _) = GetBackofficeActor(ctx);
    var result = await sender.Send(new GetActiveImpersonationSessionQuery(callerId));
    return ToHttpResult(result);
}).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.TenantsRead)).WithTags("Backoffice Impersonation");

backoffice.MapGet("/impersonation/history", async (Guid? tenantId, Guid? adminUserId, int? page, int? pageSize, ISender sender) =>
{
    var result = await sender.Send(new GetImpersonationHistoryQuery(tenantId, adminUserId, page ?? 1, pageSize ?? 20));
    return ToHttpResult(result);
}).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.AuditRead)).WithTags("Backoffice Impersonation");

// Backoffice Auth Endpoints (Anonymous / Credentials + MFA)
app.MapPost("/api/v1/backoffice/auth/login", async (BackofficeLoginRequest req, HttpContext ctx, ISender sender) =>
{
    var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
    var ua = ctx.Request.Headers.UserAgent.ToString() ?? "Unknown";
    var command = new AuthenticateAdminUserCommand(req.Email, req.Password, req.MfaCode, ip, ua);
    var result = await sender.Send(command);
    return ToBackofficeLoginHttpResult(result);
}).AllowAnonymous().WithTags("Backoffice Auth");

app.MapPost("/api/v1/backoffice/auth/refresh", async (RefreshSessionRequest req, HttpContext ctx, ISender sender) =>
{
    var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
    var ua = ctx.Request.Headers.UserAgent.ToString() ?? "Unknown";
    var command = new RefreshAdminSessionCommand(req.SessionToken, req.RefreshToken, ip, ua);
    var result = await sender.Send(command);
    return ToHttpResult(result);
}).AllowAnonymous().WithTags("Backoffice Auth");

// Auth Endpoints
app.MapPost("/api/auth/login", async (LoginCommand command, ISender sender) =>
{
    var result = await sender.Send(command);
    return result.IsSuccess 
        ? Results.Ok(result.Value) 
        : Results.Json(result.Error, statusCode: 401);
});

app.MapPost("/api/auth/register", async (RegisterUserCommand command, ISender sender) =>
{
    var result = await sender.Send(command);
    return result.IsSuccess
        ? Results.Json(result.Value, statusCode: StatusCodes.Status201Created)
        : Results.Json(result.Error, statusCode: ToStatusCode(result.Error.Type));
}).AllowAnonymous().WithTags("Auth");

app.MapPost("/api/auth/forgot-password", async (ForgotPasswordCommand command, ISender sender) =>
{
    var result = await sender.Send(command);
    return result.IsSuccess
        ? Results.Ok(new { token = result.Value, message = "Se o e-mail estiver cadastrado, as instruções para redefinição foram geradas com sucesso." })
        : Results.Json(result.Error, statusCode: ToStatusCode(result.Error.Type));
}).AllowAnonymous().WithTags("Auth");

app.MapPost("/api/auth/reset-password", async (ResetPasswordCommand command, ISender sender) =>
{
    var result = await sender.Send(command);
    return result.IsSuccess
        ? Results.Ok(new { message = "Senha redefinida com sucesso." })
        : Results.Json(result.Error, statusCode: ToStatusCode(result.Error.Type));
}).AllowAnonymous().WithTags("Auth");

app.MapPost("/api/auth/select-tenant", async (SelectTenantCommand command, ISender sender) =>
{
    var result = await sender.Send(command);
    return result.IsSuccess 
        ? Results.Ok(result.Value) 
        : Results.Json(result.Error, statusCode: 400);
});

app.MapGet("/api/v1/tenancy/plans", async (ISender sender) =>
{
    var result = await sender.Send(new GetSubscriptionPlansQuery());
    return result.IsSuccess
        ? Results.Ok(result.Value)
        : Results.Json(result.Error, statusCode: 400);
}).AllowAnonymous().WithTags("Tenancy");

app.MapPost("/api/v1/tenancy/farms", async (CreateTenantCommand command, ISender sender) =>
{
    var result = await sender.Send(command);
    return result.IsSuccess
        ? Results.Json(result.Value, statusCode: StatusCodes.Status201Created)
        : Results.Json(result.Error, statusCode: ToStatusCode(result.Error.Type));
}).AllowAnonymous().WithTags("Tenancy");

app.MapGet("/api/v1/tenancy/profile", async (Guid tenantId, ISender sender) =>
{
    var result = await sender.Send(new GetTenantProfileQuery(tenantId));
    return ToHttpResult(result);
}).RequireAuthorization().WithTags("Tenancy");

app.MapPut("/api/v1/tenancy/profile", async (UpdateTenantProfileCommand command, ISender sender) =>
{
    var result = await sender.Send(command);
    return ToHttpResult(result);
}).RequireAuthorization().WithTags("Tenancy");

app.MapPut("/api/v1/tenancy/subscription", async (ChangeSubscriptionPlanRequest request, System.Security.Claims.ClaimsPrincipal userClaims, ISender sender) =>
{
    var sub = userClaims.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
           ?? userClaims.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value 
           ?? userClaims.FindFirst("sub")?.Value;

    if (!Guid.TryParse(sub, out var userId))
    {
        return Results.Unauthorized();
    }

    var command = new ChangeSubscriptionPlanCommand(request.TenantId, userId, request.NewPlan);
    var result = await sender.Send(command);
    return ToHttpResult(result);
}).RequireAuthorization().WithTags("Tenancy");

app.MapGet("/api/v1/tenancy/production-units", async (Guid tenantId, ISender sender) =>
{
    var result = await sender.Send(new GetProductionUnitsQuery(tenantId));
    return ToHttpResult(result);
}).RequireAuthorization().WithTags("Tenancy");

app.MapPost("/api/v1/tenancy/production-units", async (CreateProductionUnitCommand command, ISender sender) =>
{
    var result = await sender.Send(command);
    return ToHttpResult(result, StatusCodes.Status201Created);
}).RequireAuthorization().WithTags("Tenancy");

app.MapPut("/api/v1/tenancy/production-units/{id:guid}", async (Guid id, UpdateProductionUnitCommand command, ISender sender) =>
{
    var result = await sender.Send(command with { Id = id });
    return ToHttpResult(result);
}).RequireAuthorization().WithTags("Tenancy");

// Team & RBAC Endpoints
app.MapGet("/api/v1/tenancy/members", async (Guid tenantId, ISender sender) =>
{
    var result = await sender.Send(new GetTeamMembersQuery(tenantId));
    return ToHttpResult(result);
}).RequireAuthorization().WithTags("Tenancy");

app.MapPost("/api/v1/tenancy/invites", async (InviteTeamMemberCommand command, ISender sender) =>
{
    var result = await sender.Send(command);
    return ToHttpResult(result, StatusCodes.Status201Created);
}).RequireAuthorization("AdminOnly").WithTags("Tenancy");

app.MapPost("/api/v1/tenancy/invites/accept", async (AcceptTeamInviteCommand command, ISender sender) =>
{
    var result = await sender.Send(command);
    return ToHttpResult(result);
}).AllowAnonymous().WithTags("Tenancy");

app.MapDelete("/api/v1/tenancy/invites/{inviteId:guid}", async (Guid inviteId, Guid tenantId, ISender sender) =>
{
    var result = await sender.Send(new RevokeTeamInviteCommand(tenantId, inviteId));
    return ToHttpResult(result);
}).RequireAuthorization("AdminOnly").WithTags("Tenancy");

app.MapDelete("/api/v1/tenancy/members/{userId:guid}", async (Guid userId, Guid tenantId, ISender sender) =>
{
    var result = await sender.Send(new RemoveTeamMemberCommand(tenantId, userId));
    return ToHttpResult(result);
}).RequireAuthorization("AdminOnly").WithTags("Tenancy");


// Cattle Breeding Endpoints
var breeding = app.MapGroup("/api/breeding")
    .RequireAuthorization()
    .WithTags("Breeding");

breeding.MapGet("/cows", async (
    ISender sender,
    string? search,
    ReproductiveStatus? status,
    int page = 1,
    int pageSize = 25) =>
{
    var result = await sender.Send(new ListCowsQuery(search, status, page, pageSize));
    return ToHttpResult(result);
});

breeding.MapPost("/cows", async (CreateCowCommand command, ISender sender) =>
{
    var result = await sender.Send(command);
    return ToHttpResult(result, StatusCodes.Status201Created);
});

breeding.MapGet("/cows/{id:guid}", async (Guid id, ISender sender) =>
{
    var result = await sender.Send(new GetCowQuery(id));
    return ToHttpResult(result);
});

breeding.MapPut("/cows/{id:guid}", async (Guid id, UpdateCowCommand command, ISender sender) =>
{
    if (id != command.Id)
        return Results.BadRequest(new { Error = "ID da URL diverge do ID do corpo da requisição." });

    var result = await sender.Send(command);
    return ToHttpResult(result);
});

breeding.MapGet("/bulls", async (Guid tenantId, ISender sender) =>
{
    var result = await sender.Send(new ListBullsQuery(tenantId));
    return ToHttpResult(result);
});

breeding.MapGet("/iatf-protocols", async (Guid tenantId, ISender sender) =>
{
    var result = await sender.Send(new GetIatfProtocolsQuery(tenantId));
    return ToHttpResult(result);
});

breeding.MapPost("/iatf-protocols", async (RegisterIatfProtocolCommand command, ISender sender) =>
{
    var result = await sender.Send(command);
    return ToHttpResult(result, StatusCodes.Status201Created);
});

breeding.MapPost("/diagnoses", async (RegisterPregnancyDiagnosisCommand command, ISender sender) =>
{
    var result = await sender.Send(command);
    return ToHttpResult(result, StatusCodes.Status201Created);
});

// Calving & Nursery Endpoints
var calving = app.MapGroup("/api/calving")
    .RequireAuthorization()
    .WithTags("Calving");

calving.MapGet("/records", async (Guid tenantId, ISender sender) =>
{
    var result = await sender.Send(new GetCalvingRecordsQuery(tenantId));
    return ToHttpResult(result);
});

calving.MapGet("", async (Guid tenantId, ISender sender) =>
{
    var result = await sender.Send(new GetCalvingRecordsQuery(tenantId));
    return ToHttpResult(result);
});

calving.MapPost("/calvings", async (RegisterCalvingCommand command, ISender sender) =>
{
    var result = await sender.Send(command);
    return ToHttpResult(result, StatusCodes.Status201Created);
});

calving.MapPost("/weanings", async (RegisterWeaningCommand command, ISender sender) =>
{
    var result = await sender.Send(command);
    return ToHttpResult(result, StatusCodes.Status201Created);
});

// Growth & Pasture Management Endpoints
var growth = app.MapGroup("/api/growth")
    .RequireAuthorization()
    .WithTags("Growth");

growth.MapGet("/paddocks", async (Guid tenantId, ISender sender) =>
{
    var result = await sender.Send(new GetPaddocksWithStockingRateQuery(tenantId));
    return ToHttpResult(result);
});

growth.MapPost("/paddocks", async (CreatePaddockCommand command, ISender sender) =>
{
    var result = await sender.Send(command);
    return ToHttpResult(result, StatusCodes.Status201Created);
});

growth.MapGet("/lots", async (Guid tenantId, ISender sender) =>
{
    var result = await sender.Send(new GetLotsQuery(tenantId));
    return ToHttpResult(result);
});

growth.MapPost("/lots", async (CreateLotCommand command, ISender sender) =>
{
    var result = await sender.Send(command);
    return ToHttpResult(result, StatusCodes.Status201Created);
});

growth.MapPost("/lots/move", async (MoveLotToPaddockCommand command, ISender sender) =>
{
    var result = await sender.Send(command);
    return ToHttpResult(result, StatusCodes.Status200OK);
});

growth.MapPost("/lots/{id:guid}/close", async (Guid id, Guid tenantId, ISender sender) =>
{
    var result = await sender.Send(new CloseLotCommand(id, tenantId));
    return ToHttpResult(result, StatusCodes.Status200OK);
});

growth.MapPost("/dispatch/animal", async (DispatchAnimalCommand command, ISender sender) =>
{
    var result = await sender.Send(command);
    return ToHttpResult(result, StatusCodes.Status200OK);
});

growth.MapPost("/dispatch/lot", async (DispatchLotCommand command, ISender sender) =>
{
    var result = await sender.Send(command);
    return ToHttpResult(result, StatusCodes.Status200OK);
});

growth.MapPost("/weighings", async (RecordWeighingCommand command, ISender sender) =>
{
    var result = await sender.Send(command);
    return ToHttpResult(result, StatusCodes.Status201Created);
});

growth.MapPost("/weighings/batch", async (BatchRecordWeighingCommand command, ISender sender) =>
{
    var result = await sender.Send(command);
    return ToHttpResult(result, StatusCodes.Status201Created);
});

growth.MapGet("/weighings/history/{animalTagId}", async (Guid tenantId, string animalTagId, ISender sender) =>
{
    var result = await sender.Send(new GetAnimalWeighingHistoryQuery(tenantId, animalTagId));
    return ToHttpResult(result);
});

growth.MapGet("/weighings/lot-summary/{lotId:guid}", async (Guid tenantId, Guid lotId, ISender sender) =>
{
    var result = await sender.Send(new GetLotWeighingSummaryQuery(tenantId, lotId));
    return ToHttpResult(result);
});

growth.MapGet("/weighings/recent", async (Guid tenantId, Guid? lotId, int? top, ISender sender) =>
{
    var result = await sender.Send(new GetRecentWeighingsQuery(tenantId, lotId, top ?? 50));
    return ToHttpResult(result);
});

growth.MapPost("/weighings/import", async (IFormFile file, ScaleModelEnum scaleModel, Guid tenantId, Guid? lotId, DateTime? defaultWeighingDate, decimal? defaultCarcassYield, ISender sender) =>
{
    if (file is null || file.Length == 0)
        return Results.BadRequest("Arquivo de balança não fornecido ou vazio.");

    using var ms = new MemoryStream();
    await file.CopyToAsync(ms);

    var command = new ImportWeighingFileCommand(
        ms.ToArray(),
        file.FileName,
        scaleModel,
        tenantId,
        lotId,
        defaultWeighingDate ?? DateTime.UtcNow,
        defaultCarcassYield ?? 50.0m);

    var result = await sender.Send(command);
    return ToHttpResult(result, StatusCodes.Status201Created);
}).DisableAntiforgery();

growth.MapGet("/weighings/anomalies", async (Guid tenantId, Guid? lotId, ISender sender) =>
{
    var result = await sender.Send(new GetWeighingAnomaliesQuery(tenantId, lotId));
    return ToHttpResult(result);
});

// Nutrition & Feed Management Endpoints
var nutrition = app.MapGroup("/api/nutrition")
    .RequireAuthorization()
    .WithTags("Nutrition");

nutrition.MapGet("/silos", async (Guid tenantId, ISender sender) =>
{
    var result = await sender.Send(new GetSiloStocksQuery(tenantId));
    return ToHttpResult(result);
});

nutrition.MapPost("/silos", async (CreateSiloStockCommand command, ISender sender) =>
{
    var result = await sender.Send(command);
    return ToHttpResult(result, StatusCodes.Status201Created);
});

nutrition.MapPost("/silos/restock", async (RestockSiloCommand command, ISender sender) =>
{
    var result = await sender.Send(command);
    return ToHttpResult(result);
});

nutrition.MapGet("/rations", async (Guid tenantId, ISender sender) =>
{
    var result = await sender.Send(new GetFeedRationsQuery(tenantId));
    return ToHttpResult(result);
});

nutrition.MapPost("/rations", async (CreateFeedRationCommand command, ISender sender) =>
{
    var result = await sender.Send(command);
    return ToHttpResult(result, StatusCodes.Status201Created);
});

nutrition.MapPost("/supplementation", async (RecordSupplementationCommand command, ISender sender) =>
{
    var result = await sender.Send(command);
    return ToHttpResult(result, StatusCodes.Status201Created);
});

nutrition.MapPost("/tmr-batches", async (RecordFeedlotTmrCommand command, ISender sender) =>
{
    var result = await sender.Send(command);
    return ToHttpResult(result, StatusCodes.Status201Created);
});

nutrition.MapGet("/analytics/feed-conversion", async (Guid tenantId, Guid lotId, decimal totalWeightGainKg, ISender sender) =>
{
    var result = await sender.Send(new GetFeedlotPerformanceQuery(tenantId, lotId, totalWeightGainKg));
    return ToHttpResult(result);
});

nutrition.MapGet("/analytics/cost-per-arroba", async (Guid tenantId, Guid lotId, decimal totalWeightGainKg, decimal? carcassYieldPercentage, ISender sender) =>
{
    var result = await sender.Send(new GetCostPerArrobaQuery(tenantId, lotId, totalWeightGainKg, carcassYieldPercentage));
    return ToHttpResult(result);
});

// --- SANITARY ENDPOINTS ---
var sanitary = app.MapGroup("/api/sanitary").RequireAuthorization();

sanitary.MapGet("/campaigns", async (ISender sender) =>
{
    var result = await sender.Send(new GetActiveCampaignsQuery());
    return ToHttpResult(result);
});

sanitary.MapPost("/campaigns", async (CreateVaccinationCampaignCommand command, ISender sender) =>
{
    var result = await sender.Send(command);
    return ToHttpResult(result, StatusCodes.Status201Created);
});

sanitary.MapPost("/treatments", async (ApplyTreatmentCommand command, ISender sender) =>
{
    var result = await sender.Send(command);
    return ToHttpResult(result, StatusCodes.Status201Created);
});

sanitary.MapGet("/slaughter-validation/{animalId:guid}", async (Guid animalId, ISender sender) =>
{
    var result = await sender.Send(new ValidateSlaughterEligibilityQuery(animalId));
    return ToHttpResult(result);
});

// --- EXECUTIVE ANALYTICS ENDPOINTS ---
var analytics = app.MapGroup("/api/analytics").RequireAuthorization();

analytics.MapGet("/dashboard", async (ISender sender, Guid? tenantId) =>
{
    var result = await sender.Send(new GetTenantExecutiveDashboardQuery(tenantId));
    return ToHttpResult(result);
});

analytics.MapGet("/executive-scorecard", async (
    int? totalCows,
    int? pregnantCows,
    int? calvesWeaned,
    decimal? totalPastureHectares,
    decimal? totalAnimalUnits,
    decimal? averageGpdKg,
    decimal? averageCostPerArroba,
    int? animalsUnderWithdrawal,
    Guid? tenantId,
    ISender sender) =>
{
    if (totalCows.HasValue && pregnantCows.HasValue && calvesWeaned.HasValue)
    {
        var query = new GetExecutiveAnalyticsQuery(
            totalCows.Value,
            pregnantCows.Value,
            calvesWeaned.Value,
            totalPastureHectares ?? 0m,
            totalAnimalUnits ?? 0m,
            averageGpdKg ?? 0m,
            averageCostPerArroba ?? 0m,
            animalsUnderWithdrawal ?? 0);
        var result = await sender.Send(query);
        return ToHttpResult(result);
    }

    var dashboardResult = await sender.Send(new GetTenantExecutiveDashboardQuery(tenantId));
    if (dashboardResult.IsSuccess)
    {
        return Results.Ok(dashboardResult.Value.Scorecard);
    }
    return ToHttpResult(dashboardResult);
});

analytics.MapPost("/export", async (ExportBovineReportQuery query, ISender sender) =>
{
    var result = await sender.Send(query);
    if (result.IsSuccess)
    {
        return Results.File(
            fileContents: result.Value.FileContents,
            contentType: result.Value.ContentType,
            fileDownloadName: result.Value.FileName);
    }

    return ToHttpResult(result);
});

analytics.MapPost("/export-csv", async (ExecutiveScorecardDto scorecard, ISender sender) =>
{
    var result = await sender.Send(new ExportBovineReportQuery(scorecard));
    if (result.IsSuccess)
    {
        return Results.File(
            fileContents: result.Value.FileContents,
            contentType: result.Value.ContentType,
            fileDownloadName: result.Value.FileName);
    }

    return ToHttpResult(result);
});

// --- REFERENCE DATA ENDPOINTS ---
var reference = app.MapGroup("/api/reference");

reference.MapGet("/breeds", async (ISender sender) =>
{
    var result = await sender.Send(new GetReferenceBreedsQuery());
    return ToHttpResult(result);
});

reference.MapGet("/vaccines", async (ISender sender) =>
{
    var result = await sender.Send(new GetVaccineCalendarQuery());
    return ToHttpResult(result);
});

app.Run();

static void SeedReferenceData(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    SystemDataSeeder.SeedAsync(scope.ServiceProvider).GetAwaiter().GetResult();
}

static void ApplyMigrations(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
        .CreateLogger("Startup.Migrations");

    var dbContexts = new DbContext[]
    {
        scope.ServiceProvider.GetRequiredService<FoundationDbContext>(),
        scope.ServiceProvider.GetRequiredService<TenancyDbContext>(),
        scope.ServiceProvider.GetRequiredService<BreedingDbContext>(),
        scope.ServiceProvider.GetRequiredService<CalvingDbContext>(),
        scope.ServiceProvider.GetRequiredService<GrowthDbContext>(),
        scope.ServiceProvider.GetRequiredService<NutritionDbContext>(),
        scope.ServiceProvider.GetRequiredService<SanitaryDbContext>(),
        scope.ServiceProvider.GetRequiredService<BackofficeDbContext>()
    };

    foreach (var dbContext in dbContexts)
    {
        DatabaseMigrationRunner.ApplyMigrations(dbContext, logger);
    }
}

static IResult ToCommandHttpResult(Result result, int successStatusCode = StatusCodes.Status200OK)
{
    if (result.IsSuccess)
    {
        return successStatusCode == StatusCodes.Status200OK
            ? Results.Ok()
            : Results.StatusCode(successStatusCode);
    }

    return Results.Json(result.Error, statusCode: ToStatusCode(result.Error.Type));
}

static IResult ToHttpResult<TValue>(Result<TValue> result, int successStatusCode = StatusCodes.Status200OK)
{
    if (result.IsSuccess)
    {
        return successStatusCode == StatusCodes.Status200OK
            ? Results.Ok(result.Value)
            : Results.Json(result.Value, statusCode: successStatusCode);
    }

    return Results.Json(result.Error, statusCode: ToStatusCode(result.Error.Type));
}

static IResult ToBackofficeLoginHttpResult(Result<AdminAuthResultDto> result)
{
    if (result.IsSuccess)
    {
        return ToHttpResult(result);
    }

    var statusCode = result.Error.Code switch
    {
        "Backoffice.InvalidCredentials" => StatusCodes.Status401Unauthorized,
        "Backoffice.MfaRequired" => StatusCodes.Status401Unauthorized,
        _ => ToStatusCode(result.Error.Type)
    };

    return Results.Json(result.Error, statusCode: statusCode);
}

static int ToStatusCode(ErrorType errorType) => errorType switch
{
    ErrorType.Validation => StatusCodes.Status400BadRequest,
    ErrorType.NotFound => StatusCodes.Status404NotFound,
    ErrorType.Conflict => StatusCodes.Status409Conflict,
    ErrorType.Unauthorized => StatusCodes.Status403Forbidden,
    _ => StatusCodes.Status400BadRequest
};

static (Guid AdminUserId, string AdminEmail, string IpAddress) GetBackofficeActor(HttpContext ctx)
{
    var sub = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? ctx.User.FindFirstValue("sub");
    var adminUserId = Guid.TryParse(sub, out var parsedId) ? parsedId : Guid.Empty;
    var adminEmail = ctx.User.FindFirstValue(ClaimTypes.Email)
        ?? ctx.User.FindFirstValue("email")
        ?? ctx.User.Identity?.Name
        ?? "admin@criacerto.com.br";
    var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
    return (adminUserId, adminEmail, ip);
}

public record CreateAdminUserRequest(string Name, string Email, string RawPassword, List<Guid> RoleIds);
public record UpdateAdminUserRequest(string Name, string Email, List<Guid> RoleIds);
public record ToggleStatusRequest(bool IsActive);
public record ResetPasswordRequest(string NewRawPassword);
public record EnableMfaRequest(string SecretKey, string VerificationCode, List<string> RecoveryCodes);
public record BackofficeLoginRequest(string Email, string Password, string? MfaCode);
public record RefreshSessionRequest(string SessionToken, string RefreshToken);
public record PublishPlanVersionRequest(string? ApprovalNotes);
