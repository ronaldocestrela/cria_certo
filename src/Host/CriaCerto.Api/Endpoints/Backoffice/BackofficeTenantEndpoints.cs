using CriaCerto.Api.Endpoints.Common;
using CriaCerto.Modules.Backoffice.Application.Features.Plans.Commands;
using CriaCerto.Modules.Backoffice.Application.Features.Plans.Queries;
using CriaCerto.Modules.Backoffice.Application.Features.Tenants.Commands;
using CriaCerto.Modules.Backoffice.Application.Features.Tenants.Dtos;
using CriaCerto.Modules.Backoffice.Application.Features.Tenants.Queries;
using CriaCerto.Modules.Backoffice.Application.Security;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CriaCerto.Api.Endpoints.Backoffice;

public static class BackofficeTenantEndpoints
{
    public static RouteGroupBuilder MapBackofficeTenantEndpoints(this RouteGroupBuilder backoffice)
    {
        backoffice.MapGet("/tenants", async (
            string? searchTerm,
            string? status,
            string? subscribedPlan,
            string? state,
            string? ownerSearch,
            string? sizeSegment,
            string? commercialRegion,
            string? productiveProfile,
            string? churnRisk,
            Guid[]? tagIds,
            bool? includeInactiveTags,
            DateTime? afterCreatedAtUtc,
            Guid? afterId,
            int? page,
            int? pageSize,
            ISender sender) =>
        {
            var query = new GetTenantsAdminQuery(
                searchTerm,
                status,
                subscribedPlan,
                state,
                ownerSearch,
                sizeSegment,
                commercialRegion,
                productiveProfile,
                churnRisk,
                tagIds,
                includeInactiveTags ?? false,
                afterCreatedAtUtc,
                afterId,
                page ?? 1,
                pageSize ?? 20);
            var result = await sender.Send(query);
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.TenantsRead)).WithTags("Backoffice Tenants");

        backoffice.MapGet("/tenants/export", async (
            string? searchTerm,
            string? status,
            string? subscribedPlan,
            string? state,
            string? ownerSearch,
            string? sizeSegment,
            string? commercialRegion,
            string? productiveProfile,
            string? churnRisk,
            Guid[]? tagIds,
            bool? includeInactiveTags,
            HttpContext ctx,
            ISender sender) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);
            var exportQuery = new ExportTenantsAdminQuery(
                searchTerm,
                status,
                subscribedPlan,
                state,
                ownerSearch,
                sizeSegment,
                commercialRegion,
                productiveProfile,
                churnRisk,
                tagIds,
                includeInactiveTags ?? false);

            var result = await sender.Send(exportQuery);
            if (result.IsFailure)
            {
                return Results.Json(result.Error, statusCode: EndpointResultExtensions.ToStatusCode(result.Error.Type));
            }

            await sender.Send(new ExportTenantsAdminAuditCommand(
                exportQuery, callerId, callerEmail, ip, result.Value));

            return Results.File(result.Value.Content, "text/csv", result.Value.FileName);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.TenantsRead)).WithTags("Backoffice Tenants");

        backoffice.MapGet("/tenants/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetTenantAdminDetailQuery(id));
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.TenantsRead)).WithTags("Backoffice Tenants");

        backoffice.MapPost("/tenants", async (CreateTenantAdminRequest req, HttpContext ctx, ISender sender) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);
            var command = new CreateTenantAdminCommand(
                req.Name, req.LegalName, req.CNPJ, req.ExternalIdentifier,
                req.State, req.City, req.StateRegistration, req.AreaInHectares,
                req.SubscribedPlan, req.Capacity, req.Type,
                req.TechnicalOwnerName, req.TechnicalOwnerEmail,
                req.CommercialOwnerName, req.CommercialOwnerEmail, req.OwnerUserEmail,
                callerId, callerEmail, ip,
                req.CurrentPeriodEndUtc);
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result, StatusCodes.Status201Created);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.TenantsWrite)).WithTags("Backoffice Tenants");

        backoffice.MapPut("/tenants/{id:guid}", async (Guid id, UpdateTenantAdminRequest req, HttpContext ctx, ISender sender) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);
            var command = new UpdateTenantAdminCommand(
                id, req.Name, req.LegalName, req.CNPJ, req.ExternalIdentifier,
                req.State, req.City, req.StateRegistration, req.AreaInHectares,
                req.Capacity, req.Type,
                req.TechnicalOwnerName, req.TechnicalOwnerEmail,
                req.CommercialOwnerName, req.CommercialOwnerEmail,
                callerId, callerEmail, ip,
                req.CurrentPeriodEndUtc,
                req.UpdateCurrentPeriodEnd);
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.TenantsWrite)).WithTags("Backoffice Tenants");

        backoffice.MapPost("/tenants/{id:guid}/suspend", async (Guid id, TenantLifecycleActionRequest req, HttpContext ctx, ISender sender) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);
            var role = BackofficeHelpers.GetBackofficeActorRole(ctx);
            var command = new SuspendTenantAdminCommand(id, req.Reason, callerId, callerEmail, ip, role);
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.TenantsSuspend)).WithTags("Backoffice Tenants");

        backoffice.MapPost("/tenants/{id:guid}/reactivate", async (Guid id, TenantLifecycleActionRequest req, HttpContext ctx, ISender sender) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);
            var command = new ReactivateTenantAdminCommand(id, req.Reason, callerId, callerEmail, ip);
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.TenantsSuspend)).WithTags("Backoffice Tenants");

        backoffice.MapPost("/tenants/{id:guid}/cancel", async (Guid id, TenantLifecycleActionRequest req, HttpContext ctx, ISender sender) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);
            var command = new CancelTenantAdminCommand(id, req.Reason, callerId, callerEmail, ip);
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.TenantsSuspend)).WithTags("Backoffice Tenants");

        backoffice.MapPost("/tenants/{id:guid}/archive", async (Guid id, TenantLifecycleActionRequest req, HttpContext ctx, ISender sender) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);
            var command = new ArchiveTenantAdminCommand(id, req.Reason, callerId, callerEmail, ip);
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.TenantsSuspend)).WithTags("Backoffice Tenants");

        backoffice.MapPost("/tenants/{id:guid}/protection", async (Guid id, TenantProtectionRequest req, HttpContext ctx, ISender sender) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);
            var command = new SetTenantProtectionAdminCommand(id, req.IsProtected, req.Reason, callerId, callerEmail, ip);
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.TenantsSuspend)).WithTags("Backoffice Tenants");

        backoffice.MapPut("/tenants/{id:guid}/segmentation", async (Guid id, UpdateTenantSegmentationAdminRequest req, HttpContext ctx, ISender sender) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);
            var command = new UpdateTenantSegmentationAdminCommand(
                id, req.SizeSegment, req.CommercialRegion, req.ProductiveProfile, req.ChurnRisk,
                callerId, callerEmail, ip);
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.TenantsWrite)).WithTags("Backoffice Tenants");

        backoffice.MapPut("/tenants/{id:guid}/tags", async (Guid id, ReplaceTenantTagsAdminRequest req, HttpContext ctx, ISender sender) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);
            var command = new ReplaceTenantTagsAdminCommand(id, req.TagIds, callerId, callerEmail, ip);
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.TenantsWrite)).WithTags("Backoffice Tenants");

        backoffice.MapGet("/tenants/tags", async (bool? includeInactive, ISender sender) =>
        {
            var result = await sender.Send(new GetOperationalTagsAdminQuery(includeInactive ?? false));
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.TenantsRead)).WithTags("Backoffice Tenants");

        backoffice.MapPost("/tenants/tags", async (CreateOperationalTagAdminRequest req, HttpContext ctx, ISender sender) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);
            var command = new CreateOperationalTagAdminCommand(req.Name, req.Category, req.ColorHex, callerId, callerEmail, ip);
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result, StatusCodes.Status201Created);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.TenantsWrite)).WithTags("Backoffice Tenants");

        backoffice.MapDelete("/tenants/tags/{tagId:guid}", async (Guid tagId, HttpContext ctx, ISender sender) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);
            var command = new DeactivateOperationalTagAdminCommand(tagId, callerId, callerEmail, ip);
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.TenantsWrite)).WithTags("Backoffice Tenants");

        backoffice.MapGet("/tenants/saved-filters", async (HttpContext ctx, ISender sender) =>
        {
            var (callerId, _, _) = BackofficeHelpers.GetBackofficeActor(ctx);
            var result = await sender.Send(new GetAdminSavedFiltersQuery(callerId));
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.TenantsRead)).WithTags("Backoffice Tenants");

        backoffice.MapPost("/tenants/saved-filters", async (SaveAdminFilterRequest req, HttpContext ctx, ISender sender) =>
        {
            var (callerId, _, _) = BackofficeHelpers.GetBackofficeActor(ctx);
            var command = new SaveAdminFilterCommand(callerId, req.Name, req.Filter, req.IsDefault);
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result, StatusCodes.Status201Created);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.TenantsRead)).WithTags("Backoffice Tenants");

        backoffice.MapDelete("/tenants/saved-filters/{filterId:guid}", async (Guid filterId, HttpContext ctx, ISender sender) =>
        {
            var (callerId, _, _) = BackofficeHelpers.GetBackofficeActor(ctx);
            var result = await sender.Send(new DeleteAdminFilterCommand(callerId, filterId));
            return result.IsSuccess ? Results.Ok() : Results.Json(result.Error, statusCode: EndpointResultExtensions.ToStatusCode(result.Error.Type));
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.TenantsRead)).WithTags("Backoffice Tenants");

        backoffice.MapGet("/tenants/{id:guid}/plan-preview", async (Guid id, Guid? targetPlanVersionId, string? headTierCode, string? selectedModules, ISender sender) =>
        {
            var modules = !string.IsNullOrWhiteSpace(selectedModules)
                ? selectedModules.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                : null;
            var query = new PreviewTenantPlanChangeQuery(id, targetPlanVersionId, headTierCode, modules);
            var result = await sender.Send(query);
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.TenantsRead)).WithTags("Backoffice Subscriptions");

        backoffice.MapPost("/tenants/{id:guid}/plan", async (Guid id, ChangeTenantPlanRequestDto req, HttpContext ctx, ISender sender) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);
            var command = new ChangeTenantPlanCommand(
                id,
                req.TargetPlanVersionId,
                callerId,
                req.Justification,
                req.ForceImmediate,
                req.HeadTierCode,
                req.SubscribedModules,
                callerEmail,
                ip);
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.TenantsWrite)).WithTags("Backoffice Subscriptions");

        return backoffice;
    }
}

