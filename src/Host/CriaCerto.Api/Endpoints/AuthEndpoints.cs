using System.Security.Claims;
using CriaCerto.Api.Endpoints.Common;
using CriaCerto.Modules.Tenancy.Application.Features.AnonymizeUserAccount;
using CriaCerto.Modules.Tenancy.Application.Features.ChangeUserPassword;
using CriaCerto.Modules.Tenancy.Application.Features.Compliance.Dpo;
using CriaCerto.Modules.Tenancy.Application.Features.Compliance.Dpo.Models;
using CriaCerto.Modules.Tenancy.Application.Features.ConfirmEmail;
using CriaCerto.Modules.Tenancy.Application.Features.ExportUserDataPortability;
using CriaCerto.Modules.Tenancy.Application.Features.ForgotPassword;
using CriaCerto.Modules.Tenancy.Application.Features.GetUserProfile;
using CriaCerto.Modules.Tenancy.Application.Features.Login;
using CriaCerto.Modules.Tenancy.Application.Features.RefreshToken;
using CriaCerto.Modules.Tenancy.Application.Features.RegisterUser;
using CriaCerto.Modules.Tenancy.Application.Features.ResendConfirmationEmail;
using CriaCerto.Modules.Tenancy.Application.Features.ResetPassword;
using CriaCerto.Modules.Tenancy.Application.Features.SelectTenant;
using CriaCerto.Modules.Tenancy.Application.Features.UpdateUserProfile;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;

namespace CriaCerto.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/login", async (LoginCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess 
                ? Results.Ok(result.Value) 
                : Results.Json(result.Error, statusCode: 401);
        });

        app.MapPost("/api/auth/register", async (RegisterUserCommand command, HttpContext ctx, ISender sender) =>
        {
            var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            if (ctx.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor) && !string.IsNullOrWhiteSpace(forwardedFor))
            {
                var firstIp = forwardedFor.ToString().Split(',')[0].Trim();
                if (!string.IsNullOrWhiteSpace(firstIp))
                {
                    ip = firstIp;
                }
            }

            var enrichedCommand = command with { ClientIpAddress = string.IsNullOrWhiteSpace(command.ClientIpAddress) ? ip : command.ClientIpAddress };
            var result = await sender.Send(enrichedCommand);
            return result.IsSuccess
                ? Results.Json(result.Value, statusCode: StatusCodes.Status201Created)
                : Results.Json(result.Error, statusCode: EndpointResultExtensions.ToStatusCode(result.Error.Type));
        }).AllowAnonymous().WithTags("Auth");

        app.MapPost("/api/auth/forgot-password", async (ForgotPasswordCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Ok(new { token = result.Value, message = "Se o e-mail estiver cadastrado, as instruções para redefinição foram geradas com sucesso." })
                : Results.Json(result.Error, statusCode: EndpointResultExtensions.ToStatusCode(result.Error.Type));
        }).AllowAnonymous().WithTags("Auth");

        app.MapPost("/api/auth/reset-password", async (ResetPasswordCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Ok(new { message = "Senha redefinida com sucesso." })
                : Results.Json(result.Error, statusCode: EndpointResultExtensions.ToStatusCode(result.Error.Type));
        }).AllowAnonymous().WithTags("Auth");

        app.MapPost("/api/auth/confirm-email", async (ConfirmEmailCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Ok(new { message = "E-mail confirmado com sucesso. Sua conta está ativada." })
                : Results.Json(result.Error, statusCode: EndpointResultExtensions.ToStatusCode(result.Error.Type));
        }).AllowAnonymous().WithTags("Auth");

        app.MapGet("/api/auth/confirm-email", async (string email, string token, ISender sender, IConfiguration configuration) =>
        {
            var webBaseUrl = (configuration["App:WebBaseUrl"] ?? configuration["WEB_BASE_URL"] ?? "http://localhost:8081").TrimEnd('/');
            var result = await sender.Send(new ConfirmEmailCommand(email, token));
            if (result.IsSuccess)
            {
                return Results.Redirect($"{webBaseUrl}/confirm-email?success=true&email={Uri.EscapeDataString(email)}");
            }

            return Results.Redirect($"{webBaseUrl}/confirm-email?error={Uri.EscapeDataString(result.Error.Message)}&email={Uri.EscapeDataString(email)}");
        }).AllowAnonymous().WithTags("Auth");

        app.MapPost("/api/auth/resend-confirmation", async (ResendConfirmationEmailCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Ok(new { message = "Se a conta existir e ainda não estiver ativada, um novo link de confirmação foi enviado para o seu e-mail." })
                : Results.Json(result.Error, statusCode: EndpointResultExtensions.ToStatusCode(result.Error.Type));
        }).AllowAnonymous().WithTags("Auth");

        app.MapPost("/api/auth/select-tenant", async (SelectTenantCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess 
                ? Results.Ok(result.Value) 
                : Results.Json(result.Error, statusCode: 400);
        });

        app.MapPost("/api/v1/auth/refresh-token", async (RefreshTokenRequest? req, ClaimsPrincipal userClaims, ISender sender) =>
        {
            var sub = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                   ?? userClaims.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value 
                   ?? userClaims.FindFirst("sub")?.Value
                   ?? userClaims.FindFirst("UserId")?.Value;

            if (!Guid.TryParse(sub, out var userId))
            {
                return Results.Unauthorized();
            }

            Guid tenantId = req?.TenantId ?? Guid.Empty;
            if (tenantId == Guid.Empty)
            {
                var tenantClaim = userClaims.FindFirst("TenantId")?.Value;
                if (!Guid.TryParse(tenantClaim, out tenantId))
                {
                    return Results.Json(
                        CriaCerto.BuildingBlocks.Abstractions.Results.Error.Validation("Auth.TenantRequired", "TenantId não informado e ausente nas credenciais ativas."),
                        statusCode: StatusCodes.Status400BadRequest);
                }
            }

            var command = new RefreshTokenCommand(userId, tenantId);
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization().WithTags("Auth");

        app.MapPost("/api/auth/refresh-token", async (RefreshTokenRequest? req, ClaimsPrincipal userClaims, ISender sender) =>
        {
            var sub = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                   ?? userClaims.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value 
                   ?? userClaims.FindFirst("sub")?.Value
                   ?? userClaims.FindFirst("UserId")?.Value;

            if (!Guid.TryParse(sub, out var userId))
            {
                return Results.Unauthorized();
            }

            Guid tenantId = req?.TenantId ?? Guid.Empty;
            if (tenantId == Guid.Empty)
            {
                var tenantClaim = userClaims.FindFirst("TenantId")?.Value;
                if (!Guid.TryParse(tenantClaim, out tenantId))
                {
                    return Results.Json(
                        CriaCerto.BuildingBlocks.Abstractions.Results.Error.Validation("Auth.TenantRequired", "TenantId não informado e ausente nas credenciais ativas."),
                        statusCode: StatusCodes.Status400BadRequest);
                }
            }

            var command = new RefreshTokenCommand(userId, tenantId);
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization().WithTags("Auth");

        // LGPD Profile & Security Endpoints (Art. 18, III)
        app.MapGet("/api/v1/auth/profile", async (ClaimsPrincipal userClaims, ISender sender) =>
        {
            var sub = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                   ?? userClaims.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value 
                   ?? userClaims.FindFirst("sub")?.Value
                   ?? userClaims.FindFirst("UserId")?.Value;

            if (!Guid.TryParse(sub, out var userId))
            {
                return Results.Unauthorized();
            }

            var result = await sender.Send(new GetUserProfileQuery(userId));
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization().WithTags("Auth");

        app.MapPut("/api/v1/auth/profile", async (UpdateUserProfileRequest req, ClaimsPrincipal userClaims, HttpContext ctx, ISender sender) =>
        {
            var sub = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                   ?? userClaims.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value 
                   ?? userClaims.FindFirst("sub")?.Value
                   ?? userClaims.FindFirst("UserId")?.Value;

            if (!Guid.TryParse(sub, out var userId))
            {
                return Results.Unauthorized();
            }

            var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            if (ctx.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor) && !string.IsNullOrWhiteSpace(forwardedFor))
            {
                var firstIp = forwardedFor.ToString().Split(',')[0].Trim();
                if (!string.IsNullOrWhiteSpace(firstIp))
                {
                    ip = firstIp;
                }
            }

            var command = new UpdateUserProfileCommand(userId, req.FullName, req.PhoneNumber, ip);
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization().WithTags("Auth");

        app.MapPost("/api/v1/auth/change-password", async (ChangeUserPasswordRequest req, ClaimsPrincipal userClaims, HttpContext ctx, ISender sender) =>
        {
            var sub = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                   ?? userClaims.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value 
                   ?? userClaims.FindFirst("sub")?.Value
                   ?? userClaims.FindFirst("UserId")?.Value;

            if (!Guid.TryParse(sub, out var userId))
            {
                return Results.Unauthorized();
            }

            var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            if (ctx.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor) && !string.IsNullOrWhiteSpace(forwardedFor))
            {
                var firstIp = forwardedFor.ToString().Split(',')[0].Trim();
                if (!string.IsNullOrWhiteSpace(firstIp))
                {
                    ip = firstIp;
                }
            }

            var command = new ChangeUserPasswordCommand(userId, req.CurrentPassword, req.NewPassword, req.ConfirmNewPassword, ip);
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToCommandHttpResult(result);
        }).RequireAuthorization().WithTags("Auth");

        app.MapGet("/api/v1/auth/my-data/export", async (ClaimsPrincipal userClaims, HttpContext ctx, ISender sender) =>
        {
            var sub = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                   ?? userClaims.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value 
                   ?? userClaims.FindFirst("sub")?.Value
                   ?? userClaims.FindFirst("UserId")?.Value;

            if (!Guid.TryParse(sub, out var userId))
            {
                return Results.Unauthorized();
            }

            var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            if (ctx.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor) && !string.IsNullOrWhiteSpace(forwardedFor))
            {
                var firstIp = forwardedFor.ToString().Split(',')[0].Trim();
                if (!string.IsNullOrWhiteSpace(firstIp))
                {
                    ip = firstIp;
                }
            }

            var query = new ExportUserDataPortabilityQuery(userId, ip);
            var result = await sender.Send(query);

            if (result.IsFailure)
            {
                return EndpointResultExtensions.ToHttpResult(result);
            }

            return Results.File(
                fileContents: result.Value.Content,
                contentType: result.Value.ContentType,
                fileDownloadName: result.Value.FileName);
        }).RequireAuthorization().WithTags("Auth");

        app.MapPost("/api/v1/auth/account/delete", async (AnonymizeUserAccountRequest req, ClaimsPrincipal userClaims, HttpContext ctx, ISender sender) =>
        {
            var sub = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                   ?? userClaims.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value 
                   ?? userClaims.FindFirst("sub")?.Value
                   ?? userClaims.FindFirst("UserId")?.Value;

            if (!Guid.TryParse(sub, out var userId))
            {
                return Results.Unauthorized();
            }

            var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            if (ctx.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor) && !string.IsNullOrWhiteSpace(forwardedFor))
            {
                var firstIp = forwardedFor.ToString().Split(',')[0].Trim();
                if (!string.IsNullOrWhiteSpace(firstIp))
                {
                    ip = firstIp;
                }
            }

            var command = new AnonymizeUserAccountCommand(userId, req.CurrentPassword, req.ConfirmationText, req.ExitReason, ip);
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToCommandHttpResult(result);
        }).RequireAuthorization().WithTags("Auth");

        // LGPD DPO Channel
        app.MapPost("/api/v1/compliance/dpo/requests", async (
            SubmitDataSubjectRequestRequest req,
            HttpContext ctx,
            ISender sender) =>
        {
            var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            if (ctx.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor) && !string.IsNullOrWhiteSpace(forwardedFor))
            {
                var firstIp = forwardedFor.ToString().Split(',')[0].Trim();
                if (!string.IsNullOrWhiteSpace(firstIp))
                {
                    ip = firstIp;
                }
            }

            var userAgent = ctx.Request.Headers.UserAgent.ToString();

            var command = new SubmitDataSubjectRequestCommand(
                FullName: req.FullName,
                Email: req.Email,
                PhoneNumber: req.PhoneNumber,
                DocumentNumber: req.DocumentNumber,
                RelationshipType: req.RelationshipType,
                RequestType: req.RequestType,
                Description: req.Description,
                IpAddress: ip,
                UserAgent: userAgent
            );

            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result, StatusCodes.Status201Created);
        }).AllowAnonymous().WithTags("Compliance");

        return app;
    }
}

