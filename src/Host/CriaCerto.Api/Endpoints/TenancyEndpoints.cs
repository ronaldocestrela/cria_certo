using System.Security.Claims;
using CriaCerto.Api.Endpoints.Common;
using CriaCerto.Modules.Tenancy.Application.Features.AcceptTeamInvite;
using CriaCerto.Modules.Tenancy.Application.Features.CreateProductionUnit;
using CriaCerto.Modules.Tenancy.Application.Features.CreateTenant;
using CriaCerto.Modules.Tenancy.Application.Features.GetProductionUnits;
using CriaCerto.Modules.Tenancy.Application.Features.GetSubscriptionPlans;
using CriaCerto.Modules.Tenancy.Application.Features.GetTeamMembers;
using CriaCerto.Modules.Tenancy.Application.Features.GetTenantProfile;
using CriaCerto.Modules.Tenancy.Application.Features.InviteTeamMember;
using CriaCerto.Modules.Tenancy.Application.Features.ProcessStripeWebhook;
using CriaCerto.Modules.Tenancy.Application.Features.RemoveTeamMember;
using CriaCerto.Modules.Tenancy.Application.Features.RevokeTeamInvite;
using CriaCerto.Modules.Tenancy.Application.Features.SubscriptionCheckout;
using CriaCerto.Modules.Tenancy.Application.Features.SubscriptionPortal;
using CriaCerto.Modules.Tenancy.Application.Features.UpdateModularSubscription;
using CriaCerto.Modules.Tenancy.Application.Features.UpdateProductionUnit;
using CriaCerto.Modules.Tenancy.Application.Features.UpdateTenantProfile;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CriaCerto.Api.Endpoints;

public static class TenancyEndpoints
{
    public static IEndpointRouteBuilder MapTenancyEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/tenancy/plans", async (ISender sender) =>
        {
            var result = await sender.Send(new GetSubscriptionPlansQuery());
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.Json(result.Error, statusCode: 400);
        }).AllowAnonymous().WithTags("Tenancy");

        app.MapGet("/api/v1/tenancy/catalog", async (ISender sender) =>
        {
            var result = await sender.Send(new GetModularSubscriptionCatalogQuery());
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.Json(result.Error, statusCode: 400);
        }).AllowAnonymous().WithTags("Tenancy");

        app.MapPost("/api/v1/tenancy/farms", async (CreateTenantCommand command, HttpContext httpContext, ISender sender) =>
        {
            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = httpContext.Request.Headers.UserAgent.ToString();
            var enrichedCommand = command with
            {
                OriginIpAddress = command.OriginIpAddress ?? ipAddress,
                UserAgent = command.UserAgent ?? userAgent
            };
            var result = await sender.Send(enrichedCommand);
            return result.IsSuccess
                ? Results.Json(result.Value, statusCode: StatusCodes.Status201Created)
                : Results.Json(result.Error, statusCode: EndpointResultExtensions.ToStatusCode(result.Error.Type));
        }).AllowAnonymous().WithTags("Tenancy");

        app.MapGet("/api/v1/tenancy/profile", async (Guid tenantId, ISender sender) =>
        {
            var result = await sender.Send(new GetTenantProfileQuery(tenantId));
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization().WithTags("Tenancy");

        app.MapPut("/api/v1/tenancy/profile", async (UpdateTenantProfileCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization().WithTags("Tenancy");

        app.MapPost("/api/v1/tenancy/subscription/checkout", async (CreateCheckoutSessionRequest request, ClaimsPrincipal userClaims, ISender sender) =>
        {
            var sub = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                   ?? userClaims.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value 
                   ?? userClaims.FindFirst("sub")?.Value
                   ?? userClaims.FindFirst("UserId")?.Value;

            if (!Guid.TryParse(sub, out var userId))
            {
                return Results.Unauthorized();
            }

            var command = new CreateCheckoutSessionCommand(
                request.TenantId,
                userId,
                string.IsNullOrWhiteSpace(request.HeadTierCode) ? (request.PlanId ?? "") : request.HeadTierCode,
                request.SelectedModules,
                request.BillingCycle,
                request.SuccessUrl,
                request.CancelUrl,
                request.PlanId);

            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization().WithTags("Tenancy Subscription");

        app.MapPost("/api/v1/tenancy/subscription/portal", async (CreatePortalSessionRequest request, ClaimsPrincipal userClaims, ISender sender) =>
        {
            var sub = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                   ?? userClaims.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value 
                   ?? userClaims.FindFirst("sub")?.Value
                   ?? userClaims.FindFirst("UserId")?.Value;

            if (!Guid.TryParse(sub, out var userId))
            {
                return Results.Unauthorized();
            }

            var command = new CreatePortalSessionCommand(request.TenantId, userId, request.ReturnUrl);
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization().WithTags("Tenancy Subscription");

        app.MapPost("/api/v1/tenancy/subscription/update-modular", async (UpdateModularSubscriptionRequest request, ClaimsPrincipal userClaims, ISender sender) =>
        {
            var sub = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                   ?? userClaims.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value 
                   ?? userClaims.FindFirst("sub")?.Value
                   ?? userClaims.FindFirst("UserId")?.Value;

            if (!Guid.TryParse(sub, out var userId))
            {
                return Results.Unauthorized();
            }

            var command = new UpdateModularSubscriptionCommand(
                request.TenantId,
                userId,
                request.HeadTierCode,
                request.SelectedModules,
                request.BillingCycle ?? "monthly",
                request.AllowGracePeriod);

            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization().WithTags("Tenancy Subscription");

        app.MapPost("/api/v1/payments/stripe-webhook", async (HttpRequest request, ISender sender) =>
        {
            using var reader = new StreamReader(request.Body);
            var jsonPayload = await reader.ReadToEndAsync();
            var stripeSignature = request.Headers["Stripe-Signature"].ToString();

            var command = new ProcessStripeWebhookCommand(jsonPayload, stripeSignature);
            var result = await sender.Send(command);

            return result.IsSuccess 
                ? Results.Ok(new { received = true, eventType = result.Value.EventType }) 
                : Results.BadRequest(new { error = result.Error.Message });
        }).AllowAnonymous().WithTags("Payments Webhook");

        app.MapGet("/api/v1/tenancy/production-units", async (Guid tenantId, ISender sender) =>
        {
            var result = await sender.Send(new GetProductionUnitsQuery(tenantId));
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization().WithTags("Tenancy");

        app.MapPost("/api/v1/tenancy/production-units", async (CreateProductionUnitCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result, StatusCodes.Status201Created);
        }).RequireAuthorization().WithTags("Tenancy");

        app.MapPut("/api/v1/tenancy/production-units/{id:guid}", async (Guid id, UpdateProductionUnitCommand command, ISender sender) =>
        {
            var result = await sender.Send(command with { Id = id });
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization().WithTags("Tenancy");

        // Team & RBAC Endpoints
        app.MapGet("/api/v1/tenancy/members", async (Guid tenantId, ISender sender) =>
        {
            var result = await sender.Send(new GetTeamMembersQuery(tenantId));
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization().WithTags("Tenancy");

        app.MapPost("/api/v1/tenancy/invites", async (InviteTeamMemberCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result, StatusCodes.Status201Created);
        }).RequireAuthorization("AdminOnly").WithTags("Tenancy");

        app.MapPost("/api/v1/tenancy/invites/accept", async (AcceptTeamInviteCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result);
        }).AllowAnonymous().WithTags("Tenancy");

        app.MapDelete("/api/v1/tenancy/invites/{inviteId:guid}", async (Guid inviteId, Guid tenantId, ISender sender) =>
        {
            var result = await sender.Send(new RevokeTeamInviteCommand(tenantId, inviteId));
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization("AdminOnly").WithTags("Tenancy");

        app.MapDelete("/api/v1/tenancy/members/{userId:guid}", async (Guid userId, Guid tenantId, ISender sender) =>
        {
            var result = await sender.Send(new RemoveTeamMemberCommand(tenantId, userId));
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization("AdminOnly").WithTags("Tenancy");

        return app;
    }
}

