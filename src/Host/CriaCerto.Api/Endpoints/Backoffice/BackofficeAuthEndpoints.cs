using CriaCerto.Api.Endpoints.Common;
using CriaCerto.Modules.Backoffice.Application.Features.AdminUsers.Commands;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CriaCerto.Api.Endpoints.Backoffice;

public static class BackofficeAuthEndpoints
{
    public static IEndpointRouteBuilder MapBackofficeAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/backoffice/auth/login", async (BackofficeLoginRequest req, HttpContext ctx, ISender sender) =>
        {
            var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            var ua = ctx.Request.Headers.UserAgent.ToString() ?? "Unknown";
            var command = new AuthenticateAdminUserCommand(req.Email, req.Password, req.MfaCode, ip, ua);
            var result = await sender.Send(command);
            return BackofficeHelpers.ToBackofficeLoginHttpResult(result);
        }).RequireRateLimiting("BackofficeAuthRateLimiter").AllowAnonymous().WithTags("Backoffice Auth");

        app.MapPost("/api/v1/backoffice/auth/refresh", async (RefreshSessionRequest req, HttpContext ctx, ISender sender) =>
        {
            var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            var ua = ctx.Request.Headers.UserAgent.ToString() ?? "Unknown";
            var command = new RefreshAdminSessionCommand(req.SessionToken, req.RefreshToken, ip, ua);
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireRateLimiting("BackofficeAuthRateLimiter").AllowAnonymous().WithTags("Backoffice Auth");

        return app;
    }
}

