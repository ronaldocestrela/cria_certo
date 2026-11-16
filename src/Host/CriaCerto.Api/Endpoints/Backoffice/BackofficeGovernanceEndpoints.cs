using System.Security.Claims;
using CriaCerto.Api.BackgroundServices;
using CriaCerto.Api.Endpoints.Common;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Enums;
using CriaCerto.Modules.Backoffice.Application.Features.Approvals.Commands;
using CriaCerto.Modules.Backoffice.Application.Features.Approvals.Dtos;
using CriaCerto.Modules.Backoffice.Application.Features.Approvals.Queries;
using CriaCerto.Modules.Backoffice.Application.Features.Audit.Commands;
using CriaCerto.Modules.Backoffice.Application.Features.Audit.Dtos;
using CriaCerto.Modules.Backoffice.Application.Features.Audit.Queries;
using CriaCerto.Modules.Backoffice.Application.Features.Compliance.Commands;
using CriaCerto.Modules.Backoffice.Application.Features.Compliance.Dtos;
using CriaCerto.Modules.Backoffice.Application.Features.Compliance.Queries;
using CriaCerto.Modules.Backoffice.Application.Features.Dashboard;
using CriaCerto.Modules.Backoffice.Application.Features.Impersonation.Commands;
using CriaCerto.Modules.Backoffice.Application.Features.Impersonation.Dtos;
using CriaCerto.Modules.Backoffice.Application.Features.Impersonation.Queries;
using CriaCerto.Modules.Backoffice.Application.Features.Observability.Commands;
using CriaCerto.Modules.Backoffice.Application.Features.Observability.Dtos;
using CriaCerto.Modules.Backoffice.Application.Features.Observability.Queries;
using CriaCerto.Modules.Backoffice.Application.Features.Rollout.Commands;
using CriaCerto.Modules.Backoffice.Application.Features.Rollout.Dtos;
using CriaCerto.Modules.Backoffice.Application.Features.Rollout.Queries;
using CriaCerto.Modules.Backoffice.Application.Features.Support.Commands;
using CriaCerto.Modules.Backoffice.Application.Features.Support.Dtos;
using CriaCerto.Modules.Backoffice.Application.Features.Support.Queries;
using CriaCerto.Modules.Backoffice.Application.Security;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CriaCerto.Api.Endpoints.Backoffice;

public static class BackofficeGovernanceEndpoints
{
    public static RouteGroupBuilder MapBackofficeGovernanceEndpoints(this RouteGroupBuilder backoffice)
    {
        // Dashboard KPIs
        backoffice.MapGet("/dashboard/kpis", async (ISender sender) =>
        {
            var result = await sender.Send(new GetBackofficeDashboardKpisQuery());
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.TenantsRead)).WithTags("Backoffice Dashboard");

        // Impersonation (ADR 0007)
        backoffice.MapPost("/impersonation/start", async (StartImpersonationRequest req, HttpContext ctx, ISender sender) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);
            var role = BackofficeHelpers.GetBackofficeActorRole(ctx);
            var ua = ctx.Request.Headers.UserAgent.ToString() ?? "Unknown";
            var command = new StartImpersonationSessionCommand(
                req.TargetTenantId,
                req.TargetUserId,
                req.SupportTicket,
                req.Justification,
                req.DurationMinutes,
                callerId,
                callerEmail,
                ip,
                ua,
                role);
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result, StatusCodes.Status201Created);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.ImpersonationStart)).WithTags("Backoffice Impersonation");

        backoffice.MapPost("/impersonation/stop", async (StopImpersonationRequest req, HttpContext ctx, ISender sender) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);
            var isPlatformOwner = ctx.User.HasClaim("is_platform_owner", "true") || ctx.User.IsInRole(BackofficeRoles.PlatformOwner);
            var command = new StopImpersonationSessionCommand(req.SessionId, callerId, callerEmail, ip, isPlatformOwner);
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToCommandHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.ImpersonationStop)).WithTags("Backoffice Impersonation");

        backoffice.MapGet("/impersonation/active", async (HttpContext ctx, ISender sender) =>
        {
            var (callerId, _, _) = BackofficeHelpers.GetBackofficeActor(ctx);
            var result = await sender.Send(new GetActiveImpersonationSessionQuery(callerId));
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.TenantsRead)).WithTags("Backoffice Impersonation");

        backoffice.MapGet("/impersonation/history", async (Guid? tenantId, Guid? adminUserId, int? page, int? pageSize, ISender sender) =>
        {
            var result = await sender.Send(new GetImpersonationHistoryQuery(tenantId, adminUserId, page ?? 1, pageSize ?? 20));
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.AuditRead)).WithTags("Backoffice Impersonation");

        // Support Workbench
        backoffice.MapGet("/support/tenants/{id:guid}/diagnostics", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetTenantDiagnosticsQuery(id));
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.SupportDiagnose)).WithTags("Backoffice Support");

        backoffice.MapGet("/support/playbooks", async (ISender sender) =>
        {
            var result = await sender.Send(new GetSupportPlaybooksQuery());
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.SupportDiagnose)).WithTags("Backoffice Support");

        backoffice.MapPost("/support/tenants/{id:guid}/remediation", async (Guid id, ExecuteRemediationRequest req, HttpContext ctx, ISender sender) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);
            var role = BackofficeHelpers.GetBackofficeActorRole(ctx);
            var command = new ExecuteTenantRemediationCommand(
                id,
                req.ActionType,
                req.SupportTicketId,
                req.Justification,
                callerId,
                callerEmail,
                ip,
                role);
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.SupportRemediate)).WithTags("Backoffice Support");

        // 4-Eyes Approvals (ADR 0008)
        backoffice.MapGet("/approvals", async (
            ApprovalRequestStatus? status,
            ApprovalRequestType? requestType,
            Guid? requestedByAdminUserId,
            Guid? reviewerId,
            int? page,
            int? pageSize,
            ISender sender) =>
        {
            var query = new GetApprovalRequestsQuery(status, requestType, requestedByAdminUserId, reviewerId, page ?? 1, pageSize ?? 20);
            var result = await sender.Send(query);
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.ApprovalsRequest, BackofficePermissions.ApprovalsReview)).WithTags("Backoffice Approvals");

        backoffice.MapGet("/approvals/pending-count", async (HttpContext ctx, ISender sender) =>
        {
            var (callerId, _, _) = BackofficeHelpers.GetBackofficeActor(ctx);
            var result = await sender.Send(new GetPendingApprovalsCountQuery(callerId));
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.ApprovalsRequest, BackofficePermissions.ApprovalsReview)).WithTags("Backoffice Approvals");

        backoffice.MapGet("/approvals/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetApprovalRequestByIdQuery(id));
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.ApprovalsRequest, BackofficePermissions.ApprovalsReview)).WithTags("Backoffice Approvals");

        backoffice.MapPost("/approvals", async (CreateApprovalRequestRequest req, HttpContext ctx, ISender sender) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);
            var command = new CreateApprovalRequestCommand(
                req.RequestType,
                req.Title,
                req.Justification,
                req.TargetResourceId,
                req.ImpactSummary,
                req.PayloadJson,
                callerId,
                callerEmail,
                ip,
                req.SupportTicketId,
                req.DiffJson,
                req.ExpirationHours);
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.ApprovalsRequest)).WithTags("Backoffice Approvals");

        backoffice.MapPost("/approvals/{id:guid}/approve", async (Guid id, ApproveApprovalRequestRequest req, HttpContext ctx, ISender sender) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);
            var command = new ApproveApprovalRequestCommand(
                id,
                callerId,
                callerEmail,
                ip,
                req.ReviewNotes);
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.ApprovalsReview)).WithTags("Backoffice Approvals");

        backoffice.MapPost("/approvals/{id:guid}/reject", async (Guid id, RejectApprovalRequestRequest req, HttpContext ctx, ISender sender) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);
            var command = new RejectApprovalRequestCommand(
                id,
                req.RejectionReason,
                callerId,
                callerEmail,
                ip);
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.ApprovalsReview)).WithTags("Backoffice Approvals");

        backoffice.MapPost("/approvals/{id:guid}/cancel", async (Guid id, CancelApprovalRequestRequest req, HttpContext ctx, ISender sender) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);
            var command = new CancelApprovalRequestCommand(
                id,
                callerId,
                callerEmail,
                ip,
                req.CancelReason);
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.ApprovalsRequest)).WithTags("Backoffice Approvals");

        // Forensic Audit (ADR 0009)
        backoffice.MapGet("/audit", async (
            int? pageNumber,
            int? pageSize,
            string? searchTerm,
            string? actorEmail,
            Guid? targetTenantId,
            string? action,
            AuditCategory? category,
            AuditSeverity? severity,
            DateTime? dateFromUtc,
            DateTime? dateToUtc,
            bool? includeArchived,
            ISender sender) =>
        {
            var query = new GetAuditLogsQuery(
                pageNumber ?? 1,
                pageSize ?? 25,
                searchTerm,
                actorEmail,
                targetTenantId,
                action,
                category,
                severity,
                dateFromUtc,
                dateToUtc,
                includeArchived ?? false);
            var result = await sender.Send(query);
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.AuditRead)).WithTags("Backoffice Audit");

        backoffice.MapGet("/audit/stats", async (ISender sender) =>
        {
            var result = await sender.Send(new GetAuditStatsQuery());
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.AuditRead)).WithTags("Backoffice Audit");

        backoffice.MapGet("/audit/verify", async (int? maxRecords, ISender sender) =>
        {
            var result = await sender.Send(new VerifyAuditTrailIntegrityQuery(maxRecords ?? 500));
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.AuditRead)).WithTags("Backoffice Audit");

        backoffice.MapGet("/audit/export", async (
            string? searchTerm,
            string? actorEmail,
            Guid? targetTenantId,
            AuditCategory? category,
            AuditSeverity? severity,
            DateTime? dateFromUtc,
            DateTime? dateToUtc,
            string? format,
            ISender sender) =>
        {
            var query = new ExportAuditTrailQuery(
                searchTerm,
                actorEmail,
                targetTenantId,
                category,
                severity,
                dateFromUtc,
                dateToUtc,
                format ?? "csv");
            var result = await sender.Send(query);
            if (result.IsFailure) return EndpointResultExtensions.ToHttpResult(result);
            return Results.File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.AuditRead)).WithTags("Backoffice Audit");

        backoffice.MapGet("/audit/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetAuditLogByIdQuery(id));
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.AuditRead)).WithTags("Backoffice Audit");

        backoffice.MapPost("/audit/retention/apply", async (ApplyAuditRetentionRequest req, HttpContext ctx, ISender sender) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);
            var command = new ApplyAuditRetentionPolicyCommand(
                callerId,
                callerEmail,
                ip,
                req.DryRun,
                req.CriticalRetentionDays,
                req.HighRetentionDays,
                req.MediumRetentionDays,
                req.LowRetentionDays);
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.UsersAdminManage)).WithTags("Backoffice Audit");

        // Observability & Anomaly Alerts (ADR 0010)
        backoffice.MapGet("/observability/health", async (ISender sender) =>
        {
            var result = await sender.Send(new GetOperationalHealthQuery());
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.ObservabilityRead)).WithTags("Backoffice Observability");

        backoffice.MapGet("/observability/alerts", async (
            int? pageNumber,
            int? pageSize,
            AlertStatus? status,
            AlertSeverity? severity,
            string? searchTerm,
            string? ruleCode,
            DateTime? dateFromUtc,
            DateTime? dateToUtc,
            ISender sender) =>
        {
            var query = new GetBackofficeAlertsQuery(
                pageNumber ?? 1,
                pageSize ?? 25,
                status,
                severity,
                searchTerm,
                ruleCode,
                dateFromUtc,
                dateToUtc);
            var result = await sender.Send(query);
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.ObservabilityRead)).WithTags("Backoffice Observability");

        backoffice.MapGet("/observability/metrics", async (ISender sender) =>
        {
            var result = await sender.Send(new GetBackofficeMetricsSummaryQuery());
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.ObservabilityRead)).WithTags("Backoffice Observability");

        backoffice.MapPost("/observability/alerts/{id:guid}/acknowledge", async (Guid id, HttpContext ctx, ISender sender) =>
        {
            var (callerId, callerEmail, _) = BackofficeHelpers.GetBackofficeActor(ctx);
            var command = new AcknowledgeBackofficeAlertCommand(id, callerId, callerEmail);
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToCommandHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.ObservabilityManage)).WithTags("Backoffice Observability");

        backoffice.MapPost("/observability/alerts/{id:guid}/resolve", async (Guid id, ResolveAlertRequest req, HttpContext ctx, ISender sender) =>
        {
            var (callerId, callerEmail, _) = BackofficeHelpers.GetBackofficeActor(ctx);
            var command = new ResolveBackofficeAlertCommand(id, callerId, callerEmail, req.ResolutionNotes);
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToCommandHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.ObservabilityManage)).WithTags("Backoffice Observability");

        backoffice.MapPost("/observability/alerts/simulate", async (SimulateAlertRequest req, ISender sender) =>
        {
            var command = new SimulateBackofficeAlertCommand(req.RuleCode, req.Severity, req.Title, req.Description, req.ContextJson);
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.ObservabilityManage)).WithTags("Backoffice Observability");

        // Compliance & LGPD (ADR 0011, ADR 0023)
        backoffice.MapGet("/compliance/overview", async (ISender sender) =>
        {
            var result = await sender.Send(new GetComplianceOverviewQuery());
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.ComplianceRead)).WithTags("Backoffice Compliance");

        backoffice.MapGet("/compliance/access-trail", async (
            Guid? targetTenantId,
            string? actorEmail,
            string? eventType,
            DateTime? dateFromUtc,
            DateTime? dateToUtc,
            int? pageNumber,
            int? pageSize,
            ISender sender) =>
        {
            var query = new GetAccessTrailQuery(
                targetTenantId,
                actorEmail,
                eventType,
                dateFromUtc,
                dateToUtc,
                pageNumber ?? 1,
                pageSize ?? 25);
            var result = await sender.Send(query);
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.ComplianceRead)).WithTags("Backoffice Compliance");

        backoffice.MapGet("/compliance/access-trail/export", async (
            Guid? targetTenantId,
            string? actorEmail,
            DateTime? dateFromUtc,
            DateTime? dateToUtc,
            string? purpose,
            string? format,
            HttpContext ctx,
            ISender sender) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);
            var userAgent = ctx.Request.Headers.UserAgent.ToString();
            var role = ctx.User.FindFirst(ClaimTypes.Role)?.Value ?? "Admin";

            var request = new ExportAccessTrailRequest(
                targetTenantId,
                actorEmail,
                dateFromUtc,
                dateToUtc,
                purpose ?? "Auditoria Externa LGPD",
                format ?? "CSV");

            var query = new ExportAccessTrailQuery(callerId, callerEmail, role, ip, userAgent, request);
            var result = await sender.Send(query);

            if (result.IsFailure)
            {
                return EndpointResultExtensions.ToHttpResult(result);
            }

            return Results.File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.ComplianceExport)).WithTags("Backoffice Compliance");

        backoffice.MapPost("/compliance/reveal-pii", async (RevealSensitiveDataRequest req, HttpContext ctx, ISender sender) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);
            var userAgent = ctx.Request.Headers.UserAgent.ToString();
            var role = BackofficeHelpers.GetBackofficeActorRole(ctx);

            var command = new RevealSensitiveDataCommand(callerId, callerEmail, role, ip, userAgent, req);
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.ComplianceUnmask)).WithTags("Backoffice Compliance");

        backoffice.MapPost("/compliance/data-retention/run-purge", async (DataRetentionPurgingBackgroundService purgeWorker, bool? dryRun, CancellationToken ct) =>
        {
            var result = await purgeWorker.ProcessDataPurgePassSafelyAsync(dryRun: dryRun, cancellationToken: ct);
            return result is not null ? Results.Ok(result) : Results.StatusCode(500);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.UsersAdminManage)).WithTags("Backoffice Compliance");

        // Wave Rollout & Feature Flags (ADR 0013)
        backoffice.MapGet("/feature-flags", async (ISender sender) =>
        {
            var result = await sender.Send(new GetFeatureFlagsQuery());
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.RolloutRead)).WithTags("Backoffice Rollout");

        backoffice.MapGet("/feature-flags/{key}", async (string key, ISender sender) =>
        {
            var result = await sender.Send(new GetFeatureFlagByKeyQuery(key));
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.RolloutRead)).WithTags("Backoffice Rollout");

        backoffice.MapGet("/feature-flags/slo-health", async (ISender sender) =>
        {
            var result = await sender.Send(new GetRolloutSloHealthQuery());
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.RolloutRead)).WithTags("Backoffice Rollout");

        backoffice.MapPost("/feature-flags/{key}/toggle", async (string key, ToggleFeatureFlagRequest req, HttpContext ctx, ISender sender) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);
            var userAgent = ctx.Request.Headers.UserAgent.ToString();
            var role = ctx.User.FindFirst(ClaimTypes.Role)?.Value ?? "Admin";

            var command = new ToggleFeatureFlagCommand(
                key,
                req.IsEnabled,
                req.Reason ?? "Alteração de estado operacional",
                callerId,
                callerEmail,
                role,
                ip,
                userAgent
            );

            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.RolloutManage)).WithTags("Backoffice Rollout");

        backoffice.MapPut("/feature-flags/{key}/rollout", async (string key, UpdateRolloutWaveRequest req, HttpContext ctx, ISender sender) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);
            var userAgent = ctx.Request.Headers.UserAgent.ToString();
            var role = ctx.User.FindFirst(ClaimTypes.Role)?.Value ?? "Admin";

            var command = new UpdateRolloutWaveCommand(
                key,
                req.Percentage,
                req.MaxAllowedRing,
                req.WhitelistedEmails,
                req.Reason ?? "Atualização da onda de liberação",
                callerId,
                callerEmail,
                role,
                ip,
                userAgent
            );

            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.RolloutManage)).WithTags("Backoffice Rollout");

        backoffice.MapPost("/feature-flags/{key}/kill-switch", async (string key, TriggerKillSwitchRequest req, HttpContext ctx, ISender sender) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);
            var userAgent = ctx.Request.Headers.UserAgent.ToString();
            var role = ctx.User.FindFirst(ClaimTypes.Role)?.Value ?? "Admin";

            var command = new TriggerKillSwitchCommand(
                key,
                req.Reason,
                callerId,
                callerEmail,
                role,
                ip,
                userAgent
            );

            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.RolloutKillSwitch)).WithTags("Backoffice Rollout");

        backoffice.MapPost("/feature-flags/{key}/restore", async (string key, RestoreKillSwitchRequest req, HttpContext ctx, ISender sender) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);
            var userAgent = ctx.Request.Headers.UserAgent.ToString();
            var role = ctx.User.FindFirst(ClaimTypes.Role)?.Value ?? "Admin";

            var command = new DeactivateKillSwitchCommand(
                key,
                req.Reason,
                callerId,
                callerEmail,
                role,
                ip,
                userAgent
            );

            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.RolloutManage)).WithTags("Backoffice Rollout");

        return backoffice;
    }
}

