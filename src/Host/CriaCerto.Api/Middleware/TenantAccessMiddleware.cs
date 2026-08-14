using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.BuildingBlocks.Abstractions.Tenancy;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Domain.Errors;

namespace CriaCerto.Api.Middleware;

public class TenantAccessMiddleware
{
    private readonly RequestDelegate _next;

    public TenantAccessMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ITenantContext tenantContext,
        ITenantAccessGuard tenantAccessGuard)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        if (path.StartsWith("/api/v1/backoffice", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/v1/tenancy/farms", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/v1/tenancy/plans", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (!tenantContext.TenantId.HasValue)
        {
            await _next(context);
            return;
        }

        var accessResult = await tenantAccessGuard.EnsureProducerAccessAsync(
            tenantContext.TenantId.Value,
            context.RequestAborted);

        if (accessResult.IsFailure)
        {
            context.Response.StatusCode = accessResult.Error.Type switch
            {
                ErrorType.NotFound => StatusCodes.Status404NotFound,
                ErrorType.Unauthorized => StatusCodes.Status403Forbidden,
                _ => StatusCodes.Status403Forbidden
            };

            await context.Response.WriteAsJsonAsync(accessResult.Error);
            return;
        }

        await _next(context);
    }
}

public static class TenantAccessMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantAccess(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<TenantAccessMiddleware>();
    }
}
