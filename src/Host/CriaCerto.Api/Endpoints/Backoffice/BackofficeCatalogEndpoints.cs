using CriaCerto.Api.Endpoints.Common;
using CriaCerto.Modules.Backoffice.Application.Features.Plans.Commands;
using CriaCerto.Modules.Backoffice.Application.Features.Plans.Queries;
using CriaCerto.Modules.Backoffice.Application.Security;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CriaCerto.Api.Endpoints.Backoffice;

public static class BackofficeCatalogEndpoints
{
    public static RouteGroupBuilder MapBackofficeCatalogEndpoints(this RouteGroupBuilder backoffice)
    {
        // Plans
        backoffice.MapGet("/plans", async (bool? includeArchived, ISender sender) =>
        {
            var result = await sender.Send(new GetPlanCatalogsQuery(includeArchived ?? false));
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.PlansRead)).WithTags("Backoffice Plans");

        backoffice.MapGet("/plans/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetPlanCatalogByIdQuery(id));
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.PlansRead)).WithTags("Backoffice Plans");

        backoffice.MapGet("/plans/versions/compare", async (Guid baseVersionId, Guid targetVersionId, ISender sender) =>
        {
            var result = await sender.Send(new ComparePlanVersionsQuery(baseVersionId, targetVersionId));
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.PlansRead)).WithTags("Backoffice Plans");

        backoffice.MapPost("/plans", async (CreatePlanCatalogCommand req, HttpContext ctx, ISender sender) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);
            var command = req with { PerformedByAdminUserId = callerId, PerformedByAdminEmail = callerEmail, IpAddress = ip };
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result, StatusCodes.Status201Created);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.PlansWrite)).WithTags("Backoffice Plans");

        backoffice.MapPost("/plans/{id:guid}/versions", async (Guid id, CreatePlanVersionCommand req, HttpContext ctx, ISender sender) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);
            var command = req with { PlanCatalogId = id, PerformedByAdminUserId = callerId, PerformedByAdminEmail = callerEmail, IpAddress = ip };
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result, StatusCodes.Status201Created);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.PlansWrite)).WithTags("Backoffice Plans");

        backoffice.MapPut("/plans/versions/{versionId:guid}", async (Guid versionId, UpdateDraftPlanVersionCommand req, HttpContext ctx, ISender sender) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);
            var command = req with { VersionId = versionId, PerformedByAdminUserId = callerId, PerformedByAdminEmail = callerEmail, IpAddress = ip };
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.PlansWrite)).WithTags("Backoffice Plans");

        backoffice.MapPost("/plans/versions/{versionId:guid}/publish", async (Guid versionId, PublishPlanVersionRequest req, HttpContext ctx, ISender sender) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);
            var role = BackofficeHelpers.GetBackofficeActorRole(ctx);
            var command = new PublishPlanVersionCommand(versionId, req.ApprovalNotes, callerId, callerEmail, ip, role);
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.PlansPublish)).WithTags("Backoffice Plans");

        // Modular Self-Service Catalog (ADR 0027)
        backoffice.MapGet("/catalog/tiers", async (bool? includeInactive, ISender sender) =>
        {
            var result = await sender.Send(new GetHeadTierCatalogsQuery(includeInactive ?? false));
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.PlansRead)).WithTags("Backoffice Catalog");

        backoffice.MapPost("/catalog/tiers", async (CreateHeadTierCatalogCommand req, HttpContext ctx, ISender sender) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);
            var command = req with { PerformedByAdminUserId = callerId, PerformedByAdminEmail = callerEmail, IpAddress = ip };
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result, StatusCodes.Status201Created);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.PlansWrite)).WithTags("Backoffice Catalog");

        backoffice.MapPut("/catalog/tiers/{tierId:guid}", async (Guid tierId, UpdateHeadTierCatalogCommand req, HttpContext ctx, ISender sender) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);
            var command = req with { TierId = tierId, PerformedByAdminUserId = callerId, PerformedByAdminEmail = callerEmail, IpAddress = ip };
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.PlansWrite)).WithTags("Backoffice Catalog");

        backoffice.MapGet("/catalog/modules", async (bool? includeInactive, ISender sender) =>
        {
            var result = await sender.Send(new GetModuleCatalogsQuery(includeInactive ?? false));
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.PlansRead)).WithTags("Backoffice Catalog");

        backoffice.MapGet("/catalog/modules/{moduleId:guid}", async (Guid moduleId, ISender sender) =>
        {
            var result = await sender.Send(new GetModuleCatalogByIdQuery(moduleId));
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.PlansRead)).WithTags("Backoffice Catalog");

        backoffice.MapPost("/catalog/modules", async (CreateModuleCatalogCommand req, HttpContext ctx, ISender sender) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);
            var command = req with { PerformedByAdminUserId = callerId, PerformedByAdminEmail = callerEmail, IpAddress = ip };
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result, StatusCodes.Status201Created);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.PlansWrite)).WithTags("Backoffice Catalog");

        backoffice.MapPut("/catalog/modules/{moduleId:guid}", async (Guid moduleId, UpdateModuleCatalogCommand req, HttpContext ctx, ISender sender) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);
            var command = req with { ModuleId = moduleId, PerformedByAdminUserId = callerId, PerformedByAdminEmail = callerEmail, IpAddress = ip };
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.PlansWrite)).WithTags("Backoffice Catalog");

        backoffice.MapPost("/catalog/modules/{moduleId:guid}/prices", async (Guid moduleId, CreateModuleTierPriceDraftCommand req, HttpContext ctx, ISender sender) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);
            var command = req with { ModuleCatalogId = moduleId, PerformedByAdminUserId = callerId, PerformedByAdminEmail = callerEmail, IpAddress = ip };
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result, StatusCodes.Status201Created);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.PlansWrite)).WithTags("Backoffice Catalog");

        backoffice.MapPut("/catalog/modules/prices/{priceId:guid}", async (Guid priceId, UpdateModuleTierPriceDraftCommand req, HttpContext ctx, ISender sender) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);
            var command = req with { PriceId = priceId, PerformedByAdminUserId = callerId, PerformedByAdminEmail = callerEmail, IpAddress = ip };
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.PlansWrite)).WithTags("Backoffice Catalog");

        backoffice.MapPost("/catalog/modules/prices/{priceId:guid}/publish", async (Guid priceId, PublishPlanVersionRequest req, HttpContext ctx, ISender sender) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);
            var role = BackofficeHelpers.GetBackofficeActorRole(ctx);
            var command = new PublishModuleTierPriceCommand(priceId, req.ApprovalNotes, callerId, callerEmail, ip, role);
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.PlansPublish)).WithTags("Backoffice Catalog");

        return backoffice;
    }
}

