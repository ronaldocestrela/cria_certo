using CriaCerto.Api.Endpoints.Common;
using CriaCerto.Modules.Backoffice.Application.Features.AdminUsers.Commands;
using CriaCerto.Modules.Backoffice.Application.Features.AdminUsers.Queries;
using CriaCerto.Modules.Backoffice.Application.Security;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CriaCerto.Api.Endpoints.Backoffice;

public static class BackofficeUserEndpoints
{
    public static RouteGroupBuilder MapBackofficeUserEndpoints(this RouteGroupBuilder backoffice)
    {
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
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.UsersAdminManage)).WithTags("Backoffice IAM");

        backoffice.MapGet("/users/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetAdminUserByIdQuery(id));
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.UsersAdminManage)).WithTags("Backoffice IAM");

        backoffice.MapPost("/users", async (CreateAdminUserRequest req, HttpContext ctx, ISender sender) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);
            var command = new CreateAdminUserCommand(req.Name, req.Email, req.RawPassword, req.RoleIds, callerId, callerEmail, ip);
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result, StatusCodes.Status201Created);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.UsersAdminManage)).WithTags("Backoffice IAM");

        backoffice.MapPut("/users/{id:guid}", async (Guid id, UpdateAdminUserRequest req, HttpContext ctx, ISender sender) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);
            var command = new UpdateAdminUserCommand(id, req.Name, req.Email, req.RoleIds, callerId, callerEmail, ip);
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.UsersAdminManage)).WithTags("Backoffice IAM");

        backoffice.MapPatch("/users/{id:guid}/status", async (Guid id, ToggleStatusRequest req, HttpContext ctx, ISender sender) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);
            var command = new ToggleAdminUserStatusCommand(id, req.IsActive, callerId, callerEmail, ip);
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.Ok() : Results.Json(result.Error, statusCode: EndpointResultExtensions.ToStatusCode(result.Error.Type));
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.UsersAdminManage)).WithTags("Backoffice IAM");

        backoffice.MapPost("/users/{id:guid}/reset-password", async (Guid id, ResetPasswordRequest req, HttpContext ctx, ISender sender) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);
            var command = new ResetAdminUserPasswordCommand(id, req.NewRawPassword, callerId, callerEmail, ip);
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.Ok() : Results.Json(result.Error, statusCode: EndpointResultExtensions.ToStatusCode(result.Error.Type));
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.UsersAdminManage)).WithTags("Backoffice IAM");

        // MFA Endpoints
        backoffice.MapPost("/users/{id:guid}/mfa/setup", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GenerateMfaSetupCommand(id));
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.UsersAdminManage)).WithTags("Backoffice MFA");

        backoffice.MapPost("/users/{id:guid}/mfa/enable", async (Guid id, EnableMfaRequest req, HttpContext ctx, ISender sender) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);
            var command = new EnableMfaCommand(id, req.SecretKey, req.VerificationCode, req.RecoveryCodes, callerId, callerEmail, ip);
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.Ok() : Results.Json(result.Error, statusCode: EndpointResultExtensions.ToStatusCode(result.Error.Type));
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.UsersAdminManage)).WithTags("Backoffice MFA");

        backoffice.MapPost("/users/{id:guid}/mfa/disable", async (Guid id, HttpContext ctx, ISender sender) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);
            var command = new DisableMfaCommand(id, callerId, callerEmail, ip);
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.Ok() : Results.Json(result.Error, statusCode: EndpointResultExtensions.ToStatusCode(result.Error.Type));
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.UsersAdminManage)).WithTags("Backoffice MFA");

        // Session Management Endpoints
        backoffice.MapDelete("/sessions/{sessionId:guid}", async (Guid sessionId, HttpContext ctx, ISender sender) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);
            var command = new RevokeAdminSessionCommand(sessionId, callerId, callerEmail, ip);
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.Ok() : Results.Json(result.Error, statusCode: EndpointResultExtensions.ToStatusCode(result.Error.Type));
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.UsersAdminManage)).WithTags("Backoffice Sessions");

        backoffice.MapDelete("/users/{id:guid}/sessions", async (Guid id, HttpContext ctx, ISender sender) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);
            var command = new RevokeAllUserSessionsCommand(id, callerId, callerEmail, ip);
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.Ok() : Results.Json(result.Error, statusCode: EndpointResultExtensions.ToStatusCode(result.Error.Type));
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.UsersAdminManage)).WithTags("Backoffice Sessions");

        return backoffice;
    }
}

