using System.Security.Claims;
using CriaCerto.Modules.Backoffice.Application.Security;
using Microsoft.AspNetCore.Http;

namespace CriaCerto.Modules.Backoffice.Infrastructure.Security;

public class BackofficeAccessMiddleware
{
    private readonly RequestDelegate _next;
    private const string BackofficePathPrefix = "/api/v1/backoffice";
    private const string BackofficeAuthPathPrefix = "/api/v1/backoffice/auth";

    public BackofficeAccessMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IPermissionEvaluator permissionEvaluator)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        if (path.StartsWith(BackofficePathPrefix, StringComparison.OrdinalIgnoreCase) &&
            !path.StartsWith(BackofficeAuthPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var user = context.User;

            if (user is null || user.Identity is null || !user.Identity.IsAuthenticated)
            {
                CriaCerto.Modules.Backoffice.Application.Telemetry.BackofficeTelemetry.RecordPolicyFailure("Unauthenticated", path);
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    code = "Backoffice.UnauthorizedAccess",
                    message = "Acesso negado. Autenticação de administrador necessária para acessar rotas de backoffice.",
                    type = "Unauthorized"
                });
                return;
            }

            if (user.HasClaim(c => c.Type == "is_impersonation" && c.Value.Equals("true", StringComparison.OrdinalIgnoreCase)))
            {
                var actorEmail = user.FindFirst(ClaimTypes.Email)?.Value;
                CriaCerto.Modules.Backoffice.Application.Telemetry.BackofficeTelemetry.RecordPolicyFailure("ImpersonationRestricted", path, actorEmail);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    code = "Backoffice.ImpersonationRestricted",
                    message = "Sessões de suporte assistido (impersonação) não podem acessar endpoints administrativos do backoffice.",
                    type = "Unauthorized"
                });
                return;
            }

            var hasAdminRole = BackofficeRoles.AllRoles.Any(role => user.IsInRole(role)) ||
                               user.HasClaim(c => c.Type == "is_backoffice_admin" && c.Value.Equals("true", StringComparison.OrdinalIgnoreCase)) ||
                               user.HasClaim(c => c.Type == "is_platform_owner" && c.Value.Equals("true", StringComparison.OrdinalIgnoreCase));

            if (!hasAdminRole)
            {
                var actorEmail = user.FindFirst(ClaimTypes.Email)?.Value;
                CriaCerto.Modules.Backoffice.Application.Telemetry.BackofficeTelemetry.RecordPolicyFailure("MissingAdminRole", path, actorEmail);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    code = "Backoffice.ForbiddenAccess",
                    message = "Acesso negado. Seu perfil não possui permissão para acessar recursos administrativos.",
                    type = "Unauthorized"
                });
                return;
            }
        }

        await _next(context);
    }
}
