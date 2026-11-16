using System.Security.Claims;
using CriaCerto.Api.Endpoints.Common;
using CriaCerto.BuildingBlocks.Abstractions.Tenancy;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Features.CustomerReferral.Commands;
using CriaCerto.Modules.Referral.Application.Features.CustomerReferral.Dtos;
using CriaCerto.Modules.Referral.Application.Features.CustomerReferral.Queries;
using CriaCerto.Modules.Referral.Application.Features.PartnerPortal.Commands;
using CriaCerto.Modules.Referral.Application.Features.PartnerPortal.Dtos;
using CriaCerto.Modules.Referral.Application.Features.PartnerPortal.Queries;
using CriaCerto.Modules.Referral.Application.Features.RecordReferralClick;
using CriaCerto.Modules.Referral.Application.Features.ValidateReferralCode;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CriaCerto.Api.Endpoints;

public static class ReferralEndpoints
{
    public static IEndpointRouteBuilder MapReferralEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/referral/clicks/{code}", async (string code, ISender sender) =>
        {
            var result = await sender.Send(new RecordReferralClickCommand(code));
            return EndpointResultExtensions.ToCommandHttpResult(result);
        }).AllowAnonymous().WithTags("Referral");

        app.MapGet("/api/v1/referral/codes/validate/{code}", async (string code, ISender sender) =>
        {
            var result = await sender.Send(new ValidateReferralCodeQuery(code));
            return EndpointResultExtensions.ToHttpResult(result);
        }).AllowAnonymous().WithTags("Referral");

        app.MapGet("/api/v1/referral/customer/dashboard", async (
            Guid? tenantId,
            ClaimsPrincipal userClaims,
            ITenantContext tenantContext,
            ISender sender) =>
        {
            var resolvedTenantId = tenantContext.TenantId;
            if (!resolvedTenantId.HasValue && userClaims != null)
            {
                var tenantClaim = userClaims.FindFirst("TenantId")?.Value;
                if (Guid.TryParse(tenantClaim, out var tid)) resolvedTenantId = tid;
            }
            if (!resolvedTenantId.HasValue && tenantId.HasValue)
            {
                resolvedTenantId = tenantId.Value;
            }

            if (!resolvedTenantId.HasValue || resolvedTenantId.Value == Guid.Empty)
            {
                return Results.Unauthorized();
            }

            var result = await sender.Send(new GetCustomerReferralDashboardQuery(resolvedTenantId.Value));
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization().WithTags("Referral");

        app.MapPost("/api/v1/referral/customer/code/customize", async (
            CustomizeReferralCodeRequest request,
            Guid? tenantId,
            ClaimsPrincipal userClaims,
            ITenantContext tenantContext,
            ISender sender) =>
        {
            var resolvedTenantId = tenantContext.TenantId;
            if (!resolvedTenantId.HasValue && userClaims != null)
            {
                var tenantClaim = userClaims.FindFirst("TenantId")?.Value;
                if (Guid.TryParse(tenantClaim, out var tid)) resolvedTenantId = tid;
            }
            if (!resolvedTenantId.HasValue && tenantId.HasValue)
            {
                resolvedTenantId = tenantId.Value;
            }

            if (!resolvedTenantId.HasValue || resolvedTenantId.Value == Guid.Empty)
            {
                return Results.Unauthorized();
            }

            var result = await sender.Send(new CustomizeReferralCodeCommand(resolvedTenantId.Value, request.NewCode));
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization().WithTags("Referral");

        app.MapGet("/api/v1/referral/partner/dashboard", async (
            ClaimsPrincipal userClaims,
            ISender sender) =>
        {
            var userIdClaim = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                ?? userClaims.FindFirst("sub")?.Value 
                ?? userClaims.FindFirst("UserId")?.Value;

            if (!Guid.TryParse(userIdClaim, out var userId) || userId == Guid.Empty)
            {
                return Results.Unauthorized();
            }

            var result = await sender.Send(new GetPartnerPortalDashboardQuery(userId));
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization().WithTags("Referral");

        app.MapGet("/api/v1/referral/partner/statement", async (
            DateTime? startDate,
            DateTime? endDate,
            LedgerRewardStatus? status,
            string? search,
            int? page,
            int? pageSize,
            ClaimsPrincipal userClaims,
            ISender sender) =>
        {
            var userIdClaim = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                ?? userClaims.FindFirst("sub")?.Value 
                ?? userClaims.FindFirst("UserId")?.Value;

            if (!Guid.TryParse(userIdClaim, out var userId) || userId == Guid.Empty)
            {
                return Results.Unauthorized();
            }

            var result = await sender.Send(new GetPartnerCommissionStatementQuery(
                userId,
                startDate,
                endDate,
                status,
                search,
                page ?? 1,
                pageSize ?? 10));

            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization().WithTags("Referral");

        app.MapPut("/api/v1/referral/partner/pix-key", async (
            UpdatePartnerPixKeyRequest request,
            ClaimsPrincipal userClaims,
            ISender sender) =>
        {
            var userIdClaim = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                ?? userClaims.FindFirst("sub")?.Value 
                ?? userClaims.FindFirst("UserId")?.Value;

            if (!Guid.TryParse(userIdClaim, out var userId) || userId == Guid.Empty)
            {
                return Results.Unauthorized();
            }

            var result = await sender.Send(new UpdatePartnerPixKeyCommand(userId, request.PixKeyType, request.PixKey));
            return EndpointResultExtensions.ToCommandHttpResult(result);
        }).RequireAuthorization().WithTags("Referral");

        app.MapPost("/api/v1/referral/partner/campaigns", async (
            CreatePartnerCampaignCodeRequest request,
            ClaimsPrincipal userClaims,
            ISender sender) =>
        {
            var userIdClaim = userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                ?? userClaims.FindFirst("sub")?.Value 
                ?? userClaims.FindFirst("UserId")?.Value;

            if (!Guid.TryParse(userIdClaim, out var userId) || userId == Guid.Empty)
            {
                return Results.Unauthorized();
            }

            var result = await sender.Send(new CreatePartnerCampaignCodeCommand(userId, request.CampaignTitle, request.PreferredSlug));
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization().WithTags("Referral");

        return app;
    }
}

