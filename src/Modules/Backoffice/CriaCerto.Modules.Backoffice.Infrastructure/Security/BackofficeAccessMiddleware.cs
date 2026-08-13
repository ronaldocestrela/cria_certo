using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace CriaCerto.Modules.Backoffice.Infrastructure.Security;

public class BackofficeAccessMiddleware
{
    private readonly RequestDelegate _next;
    private const string BackofficePathPrefix = "/api/v1/backoffice";

    public BackofficeAccessMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        if (path.StartsWith(BackofficePathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            // Default Deny Policy Enforcement
            var user = context.User;

            if (user is null || user.Identity is null || !user.Identity.IsAuthenticated)
            {
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

            var hasAdminRole = user.IsInRole("PlatformOwner") ||
                               user.IsInRole("SupportN1") ||
                               user.IsInRole("SupportN2") ||
                               user.IsInRole("FinanceOps") ||
                               user.IsInRole("ReadOnlyAuditor") ||
                               user.HasClaim(c => c.Type == "is_backoffice_admin" && c.Value.Equals("true", StringComparison.OrdinalIgnoreCase));

            if (!hasAdminRole)
            {
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
