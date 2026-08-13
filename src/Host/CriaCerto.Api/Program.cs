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
    provisioner.RegisterTenantDbContextType(typeof(BreedingDbContext));
    provisioner.RegisterTenantDbContextType(typeof(CalvingDbContext));
    provisioner.RegisterTenantDbContextType(typeof(GrowthDbContext));
    provisioner.RegisterTenantDbContextType(typeof(NutritionDbContext));
    provisioner.RegisterTenantDbContextType(typeof(SanitaryDbContext));
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
app.UseTenantDatabase();
app.UseBackofficeModule();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "CriaCerto.Api" }))
    .WithName("Health");

using CriaCerto.Modules.Backoffice.Application.Features.AdminUsers.Commands;
using CriaCerto.Modules.Backoffice.Application.Features.AdminUsers.Queries;
using CriaCerto.Modules.Backoffice.Application.Features.AdminUsers.Dtos;
using CriaCerto.Modules.Backoffice.Application.Security;

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
    var callerId = Guid.Empty;
    var callerEmail = ctx.User.Identity?.Name ?? "admin@criacerto.com.br";
    var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
    var command = new CreateAdminUserCommand(req.Name, req.Email, req.RawPassword, req.RoleIds, callerId, callerEmail, ip);
    var result = await sender.Send(command);
    return ToHttpResult(result, StatusCodes.Status201Created);
}).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.UsersAdminManage)).WithTags("Backoffice IAM");

backoffice.MapPut("/users/{id:guid}", async (Guid id, UpdateAdminUserRequest req, HttpContext ctx, ISender sender) =>
{
    var callerId = Guid.Empty;
    var callerEmail = ctx.User.Identity?.Name ?? "admin@criacerto.com.br";
    var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
    var command = new UpdateAdminUserCommand(id, req.Name, req.Email, req.RoleIds, callerId, callerEmail, ip);
    var result = await sender.Send(command);
    return ToHttpResult(result);
}).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.UsersAdminManage)).WithTags("Backoffice IAM");

backoffice.MapPatch("/users/{id:guid}/status", async (Guid id, ToggleStatusRequest req, HttpContext ctx, ISender sender) =>
{
    var callerId = Guid.Empty;
    var callerEmail = ctx.User.Identity?.Name ?? "admin@criacerto.com.br";
    var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
    var command = new ToggleAdminUserStatusCommand(id, req.IsActive, callerId, callerEmail, ip);
    var result = await sender.Send(command);
    return result.IsSuccess ? Results.Ok() : Results.Json(result.Error, statusCode: ToStatusCode(result.Error.Type));
}).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.UsersAdminManage)).WithTags("Backoffice IAM");

backoffice.MapPost("/users/{id:guid}/reset-password", async (Guid id, ResetPasswordRequest req, HttpContext ctx, ISender sender) =>
{
    var callerId = Guid.Empty;
    var callerEmail = ctx.User.Identity?.Name ?? "admin@criacerto.com.br";
    var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
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
    var callerId = Guid.Empty;
    var callerEmail = ctx.User.Identity?.Name ?? "admin@criacerto.com.br";
    var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
    var command = new EnableMfaCommand(id, req.SecretKey, req.VerificationCode, req.RecoveryCodes, callerId, callerEmail, ip);
    var result = await sender.Send(command);
    return result.IsSuccess ? Results.Ok() : Results.Json(result.Error, statusCode: ToStatusCode(result.Error.Type));
}).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.UsersAdminManage)).WithTags("Backoffice MFA");

backoffice.MapPost("/users/{id:guid}/mfa/disable", async (Guid id, HttpContext ctx, ISender sender) =>
{
    var callerId = Guid.Empty;
    var callerEmail = ctx.User.Identity?.Name ?? "admin@criacerto.com.br";
    var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
    var command = new DisableMfaCommand(id, callerId, callerEmail, ip);
    var result = await sender.Send(command);
    return result.IsSuccess ? Results.Ok() : Results.Json(result.Error, statusCode: ToStatusCode(result.Error.Type));
}).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.UsersAdminManage)).WithTags("Backoffice MFA");

// Session Management Endpoints
backoffice.MapDelete("/sessions/{sessionId:guid}", async (Guid sessionId, HttpContext ctx, ISender sender) =>
{
    var callerId = Guid.Empty;
    var callerEmail = ctx.User.Identity?.Name ?? "admin@criacerto.com.br";
    var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
    var command = new RevokeAdminSessionCommand(sessionId, callerId, callerEmail, ip);
    var result = await sender.Send(command);
    return result.IsSuccess ? Results.Ok() : Results.Json(result.Error, statusCode: ToStatusCode(result.Error.Type));
}).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.UsersAdminManage)).WithTags("Backoffice Sessions");

backoffice.MapDelete("/users/{id:guid}/sessions", async (Guid id, HttpContext ctx, ISender sender) =>
{
    var callerId = Guid.Empty;
    var callerEmail = ctx.User.Identity?.Name ?? "admin@criacerto.com.br";
    var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
    var command = new RevokeAllUserSessionsCommand(id, callerId, callerEmail, ip);
    var result = await sender.Send(command);
    return result.IsSuccess ? Results.Ok() : Results.Json(result.Error, statusCode: ToStatusCode(result.Error.Type));
}).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.UsersAdminManage)).WithTags("Backoffice Sessions");

// Backoffice Auth Endpoints (Anonymous / Credentials + MFA)
app.MapPost("/api/v1/backoffice/auth/login", async (BackofficeLoginRequest req, HttpContext ctx, ISender sender) =>
{
    var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
    var ua = ctx.Request.Headers.UserAgent.ToString() ?? "Unknown";
    var command = new AuthenticateAdminUserCommand(req.Email, req.Password, req.MfaCode, ip, ua);
    var result = await sender.Send(command);
    return ToHttpResult(result);
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

analytics.MapGet("/executive-scorecard", async (
    int totalCows,
    int pregnantCows,
    int calvesWeaned,
    decimal totalPastureHectares,
    decimal totalAnimalUnits,
    decimal averageGpdKg,
    decimal averageCostPerArroba,
    int animalsUnderWithdrawal,
    ISender sender) =>
{
    var query = new GetExecutiveAnalyticsQuery(
        totalCows, pregnantCows, calvesWeaned, totalPastureHectares, totalAnimalUnits, averageGpdKg, averageCostPerArroba, animalsUnderWithdrawal);
    var result = await sender.Send(query);
    return ToHttpResult(result);
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
        try
        {
            if (dbContext.Database.IsRelational())
            {
                var databaseCreator = dbContext.Database.GetService<IDatabaseCreator>() as RelationalDatabaseCreator;
                if (databaseCreator != null)
                {
                    if (!databaseCreator.Exists())
                    {
                        databaseCreator.Create();
                    }

                    var defaultSchema = dbContext.Model.GetDefaultSchema();
                    if (!string.IsNullOrWhiteSpace(defaultSchema))
                    {
                        try
                        {
                            var createSchemaSql = $@"
                                IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = '{defaultSchema}')
                                BEGIN
                                    EXEC('CREATE SCHEMA [{defaultSchema}]')
                                END";
                            dbContext.Database.ExecuteSqlRaw(createSchemaSql);
                        }
                        catch
                        {
                            // Schema creation fallback if already existing or unsupported
                        }
                    }

                    try
                    {
                        databaseCreator.CreateTables();
                        logger.LogInformation("Tables created for DbContext {DbContextName}", dbContext.GetType().Name);
                    }
                    catch
                    {
                        // Tables may already exist
                    }
                }

                try
                {
                    dbContext.Database.Migrate();
                    logger.LogInformation("Migrations applied for DbContext {DbContextName}", dbContext.GetType().Name);
                }
                catch
                {
                    // Ignore migration errors if no migration history/files
                }
            }
            else
            {
                dbContext.Database.EnsureCreated();
                logger.LogInformation("EnsureCreated applied for non-relational DbContext {DbContextName}", dbContext.GetType().Name);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Note for DbContext {DbContextName} during startup migration/schema check.", dbContext.GetType().Name);
        }
    }
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

static int ToStatusCode(ErrorType errorType) => errorType switch
{
    ErrorType.Validation => StatusCodes.Status400BadRequest,
    ErrorType.NotFound => StatusCodes.Status404NotFound,
    ErrorType.Conflict => StatusCodes.Status409Conflict,
    ErrorType.Unauthorized => StatusCodes.Status403Forbidden,
    _ => StatusCodes.Status400BadRequest
};

public record CreateAdminUserRequest(string Name, string Email, string RawPassword, List<Guid> RoleIds);
public record UpdateAdminUserRequest(string Name, string Email, List<Guid> RoleIds);
public record ToggleStatusRequest(bool IsActive);
public record ResetPasswordRequest(string NewRawPassword);
public record EnableMfaRequest(string SecretKey, string VerificationCode, List<string> RecoveryCodes);
public record BackofficeLoginRequest(string Email, string Password, string? MfaCode);
public record RefreshSessionRequest(string SessionToken, string RefreshToken);
