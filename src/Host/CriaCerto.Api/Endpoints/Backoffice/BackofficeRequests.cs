using System.Security.Claims;
using CriaCerto.Api.Endpoints.Common;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Features.AdminUsers.Dtos;
using CriaCerto.Modules.Backoffice.Application.Security;
using Microsoft.AspNetCore.Http;

namespace CriaCerto.Api.Endpoints.Backoffice;

public record CreateAdminUserRequest(string Name, string Email, string RawPassword, List<Guid> RoleIds);
public record UpdateAdminUserRequest(string Name, string Email, List<Guid> RoleIds);
public record ToggleStatusRequest(bool IsActive);
public record ResetPasswordRequest(string NewRawPassword);
public record EnableMfaRequest(string SecretKey, string VerificationCode, List<string> RecoveryCodes);
public record BackofficeLoginRequest(string Email, string Password, string? MfaCode);
public record RefreshSessionRequest(string SessionToken, string RefreshToken);
public record PublishPlanVersionRequest(string? ApprovalNotes);

public static class BackofficeHelpers
{
    public static (Guid AdminUserId, string AdminEmail, string IpAddress) GetBackofficeActor(HttpContext ctx)
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

    public static string GetBackofficeActorRole(HttpContext ctx)
    {
        var roles = ctx.User.FindAll(ClaimTypes.Role).Select(c => c.Value)
            .Concat(ctx.User.FindAll("role").Select(c => c.Value))
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (roles.Contains(BackofficeRoles.PlatformOwner, StringComparer.OrdinalIgnoreCase) ||
            ctx.User.HasClaim("is_platform_owner", "true"))
        {
            return BackofficeRoles.PlatformOwner;
        }

        if (roles.Contains(BackofficeRoles.FinanceOps, StringComparer.OrdinalIgnoreCase))
        {
            return BackofficeRoles.FinanceOps;
        }

        if (roles.Contains("Admin", StringComparer.OrdinalIgnoreCase) ||
            roles.Contains("PlatformAdmin", StringComparer.OrdinalIgnoreCase))
        {
            return "Admin";
        }

        if (roles.Contains(BackofficeRoles.SupportN2, StringComparer.OrdinalIgnoreCase))
        {
            return BackofficeRoles.SupportN2;
        }

        if (roles.Contains(BackofficeRoles.SupportN1, StringComparer.OrdinalIgnoreCase))
        {
            return BackofficeRoles.SupportN1;
        }

        return roles.FirstOrDefault() ?? BackofficeRoles.PlatformOwner;
    }

    public static IResult ToBackofficeLoginHttpResult(Result<AdminAuthResultDto> result)
    {
        if (result.IsSuccess)
        {
            return EndpointResultExtensions.ToHttpResult(result);
        }

        var statusCode = result.Error.Code switch
        {
            "Backoffice.InvalidCredentials" => StatusCodes.Status401Unauthorized,
            "Backoffice.MfaRequired" => StatusCodes.Status401Unauthorized,
            _ => EndpointResultExtensions.ToErrorStatusCode(result.Error)
        };

        return Results.Json(result.Error, statusCode: statusCode);
    }
}

