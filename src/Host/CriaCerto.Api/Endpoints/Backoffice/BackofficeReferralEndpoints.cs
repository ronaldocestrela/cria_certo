using System.Text.Json;
using CriaCerto.Api.Endpoints.Common;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Features.Approvals.Commands;
using CriaCerto.Modules.Backoffice.Application.Features.PaidMedia.Commands;
using CriaCerto.Modules.Backoffice.Application.Features.PaidMedia.Dtos;
using CriaCerto.Modules.Backoffice.Application.Features.PaidMedia.Queries;
using CriaCerto.Modules.Backoffice.Application.Security;
using CriaCerto.Modules.Referral.Application.Abstractions;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Features.Analytics.Queries;
using CriaCerto.Modules.Referral.Application.Features.CommercialPartners.Commands;
using CriaCerto.Modules.Referral.Application.Features.CommercialPartners.Dtos;
using CriaCerto.Modules.Referral.Application.Features.CommercialPartners.Queries;
using CriaCerto.Modules.Referral.Application.Features.Enforcement.Commands;
using CriaCerto.Modules.Referral.Application.Features.Payouts.Commands;
using CriaCerto.Modules.Referral.Application.Features.Payouts.Dtos;
using CriaCerto.Modules.Referral.Application.Features.Payouts.Queries;
using CriaCerto.Modules.Referral.Application.Features.PlanDiscounts.Commands;
using CriaCerto.Modules.Referral.Application.Features.PlanDiscounts.Dtos;
using CriaCerto.Modules.Referral.Application.Features.PlanDiscounts.Queries;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Api.Endpoints.Backoffice;

public static class BackofficeReferralEndpoints
{
    public static RouteGroupBuilder MapBackofficeReferralEndpoints(this RouteGroupBuilder backoffice)
    {
        // Commercial Partners
        backoffice.MapGet("/partners", async (
            string? searchTerm,
            ParticipantStatus? status,
            CommissionType? commissionType,
            int? page,
            int? pageSize,
            ISender sender) =>
        {
            var query = new GetCommercialPartnersQuery(searchTerm, status, commissionType, page ?? 1, pageSize ?? 20);
            var result = await sender.Send(query);
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.ReferralPartnersRead)).WithTags("Backoffice Referral Partners");

        backoffice.MapGet("/partners/suggest-code", async (string name, ISender sender) =>
        {
            var query = new SuggestReferralCodeQuery(name);
            var result = await sender.Send(query);
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.ReferralPartnersRead)).WithTags("Backoffice Referral Partners");

        backoffice.MapGet("/partners/{id:guid}", async (Guid id, ISender sender) =>
        {
            var query = new GetCommercialPartnerByIdQuery(id);
            var result = await sender.Send(query);
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.ReferralPartnersRead)).WithTags("Backoffice Referral Partners");

        backoffice.MapPost("/partners", async (RegisterPartnerRequest req, HttpContext ctx, ISender sender, DbContext dbContext) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);
            var command = new RegisterCommercialPartnerCommand(
                req.FullName,
                req.TradeName,
                req.Email,
                req.PhoneNumber,
                req.Category,
                req.City,
                req.State,
                req.Notes,
                req.LegalDocumentNumber,
                req.PixKeyType,
                req.PixKey,
                req.CommissionType,
                req.CommissionValue,
                req.MaxRecurringMonths,
                req.PaymentTermsDays ?? 30,
                req.PreferredReferralCode,
                callerId,
                callerEmail,
                ip);

            var result = await sender.Send(command);

            if (result.IsSuccess)
            {
                var audit = AuditLog.Create(
                    callerId,
                    callerEmail,
                    "Referral.PartnerRegistered",
                    $"Partner/{result.Value.ParticipantId}",
                    ip,
                    JsonSerializer.Serialize(new
                    {
                        result.Value.ParticipantId,
                        result.Value.FullName,
                        result.Value.Email,
                        result.Value.ReferralCode,
                        result.Value.CommissionType,
                        result.Value.CommissionValue
                    }));

                dbContext.Set<AuditLog>().Add(audit);
                await dbContext.SaveChangesAsync();
            }

            return EndpointResultExtensions.ToHttpResult(result, StatusCodes.Status201Created);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.ReferralPartnersManage)).WithTags("Backoffice Referral Partners");

        backoffice.MapPut("/partners/{id:guid}/profile", async (Guid id, UpdatePartnerProfileRequest req, HttpContext ctx, ISender sender, DbContext dbContext) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);
            var command = new UpdateCommercialPartnerProfileCommand(
                id,
                req.FullName,
                req.TradeName,
                req.Email,
                req.PhoneNumber,
                req.Category,
                req.City,
                req.State,
                req.Notes,
                req.PixKey,
                req.PixKeyType);

            var result = await sender.Send(command);

            if (result.IsSuccess)
            {
                var audit = AuditLog.Create(
                    callerId,
                    callerEmail,
                    "Referral.PartnerProfileUpdated",
                    $"Partner/{id}",
                    ip,
                    JsonSerializer.Serialize(new { ParticipantId = id, req.FullName, req.Email }));

                dbContext.Set<AuditLog>().Add(audit);
                await dbContext.SaveChangesAsync();
            }

            return EndpointResultExtensions.ToCommandHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.ReferralPartnersManage)).WithTags("Backoffice Referral Partners");

        backoffice.MapPost("/partners/{id:guid}/contracts/propose-update", async (Guid id, ProposeContractChangeRequest req, HttpContext ctx, ISender sender) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);
            var payloadJson = JsonSerializer.Serialize(new
            {
                ParticipantId = id,
                CommissionType = req.CommissionType.ToString(),
                CommissionValue = req.CommissionValue,
                MaxRecurringMonths = req.MaxRecurringMonths,
                PaymentTermsDays = req.PaymentTermsDays ?? 30,
                PixKey = req.PixKey,
                PixKeyType = req.PixKeyType?.ToString()
            });

            var command = new CreateApprovalRequestCommand(
                ApprovalRequestType.UpdatePartnerContractCommission,
                $"Ajuste de Comissão do Parceiro {id}",
                req.Justification,
                $"Partner/{id}",
                $"Alteração para {req.CommissionType} - {req.CommissionValue:N2}",
                payloadJson,
                callerId,
                callerEmail,
                ip);

            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result, StatusCodes.Status202Accepted);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.ApprovalsRequest)).WithTags("Backoffice Referral Partners");

        backoffice.MapPost("/partners/{id:guid}/suspend", async (Guid id, SuspendPartnerRequest req, HttpContext ctx, ISender sender, DbContext dbContext) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);
            var command = new SuspendCommercialPartnerCommand(id, req.Reason, callerId);
            var result = await sender.Send(command);

            if (result.IsSuccess)
            {
                var audit = AuditLog.Create(
                    callerId,
                    callerEmail,
                    "Referral.PartnerSuspended",
                    $"Partner/{id}",
                    ip,
                    JsonSerializer.Serialize(new { ParticipantId = id, req.Reason }));

                dbContext.Set<AuditLog>().Add(audit);
                await dbContext.SaveChangesAsync();
            }

            return EndpointResultExtensions.ToCommandHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.ReferralPartnersManage)).WithTags("Backoffice Referral Partners");

        backoffice.MapPost("/partners/{id:guid}/reactivate", async (Guid id, HttpContext ctx, ISender sender, DbContext dbContext) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);
            var command = new ReactivateCommercialPartnerCommand(id, callerId);
            var result = await sender.Send(command);

            if (result.IsSuccess)
            {
                var audit = AuditLog.Create(
                    callerId,
                    callerEmail,
                    "Referral.PartnerReactivated",
                    $"Partner/{id}",
                    ip,
                    JsonSerializer.Serialize(new { ParticipantId = id }));

                dbContext.Set<AuditLog>().Add(audit);
                await dbContext.SaveChangesAsync();
            }

            return EndpointResultExtensions.ToCommandHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.ReferralPartnersManage)).WithTags("Backoffice Referral Partners");

        // Referral Payouts & Commissions
        backoffice.MapGet("/referral/payouts/kpis", async (ISender sender) =>
        {
            var query = new GetPayoutDashboardKpisQuery();
            var result = await sender.Send(query);
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.ReferralPayoutsRead)).WithTags("Backoffice Referral Payouts");

        backoffice.MapGet("/referral/payouts/pending", async (string? search, ISender sender) =>
        {
            var query = new GetPendingMaturityCommissionsQuery(search);
            var result = await sender.Send(query);
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.ReferralPayoutsRead)).WithTags("Backoffice Referral Payouts");

        backoffice.MapGet("/referral/payouts/available", async (string? search, ISender sender) =>
        {
            var query = new GetAvailablePartnerPayoutsQuery(search);
            var result = await sender.Send(query);
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.ReferralPayoutsRead)).WithTags("Backoffice Referral Payouts");

        backoffice.MapGet("/referral/payouts/history", async (string? search, int? page, int? pageSize, ISender sender) =>
        {
            var query = new GetPayoutHistoryQuery(search, page ?? 1, pageSize ?? 50);
            var result = await sender.Send(query);
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.ReferralPayoutsRead)).WithTags("Backoffice Referral Payouts");

        backoffice.MapGet("/referral/payouts/export-csv", async (string? participantIds, ISender sender) =>
        {
            IReadOnlyList<Guid>? parsedIds = null;
            if (!string.IsNullOrWhiteSpace(participantIds))
            {
                parsedIds = participantIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(id => Guid.TryParse(id, out var g) ? g : Guid.Empty)
                    .Where(g => g != Guid.Empty)
                    .ToList();
            }

            var query = new ExportPixBatchCsvQuery(parsedIds);
            var result = await sender.Send(query);

            if (result.IsFailure)
            {
                return EndpointResultExtensions.ToHttpResult(result);
            }

            return Results.File(result.Value.FileBytes, result.Value.ContentType, result.Value.FileName);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.ReferralPayoutsExport)).WithTags("Backoffice Referral Payouts");

        backoffice.MapGet("/referral/payouts/receipts/{fileName}", async (string fileName, ISender sender) =>
        {
            var query = new GetPayoutReceiptFileQuery(fileName);
            var result = await sender.Send(query);

            if (result.IsFailure)
            {
                return EndpointResultExtensions.ToHttpResult(result);
            }

            return Results.File(result.Value.Stream, result.Value.ContentType, result.Value.DownloadFileName);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.ReferralPayoutsRead)).WithTags("Backoffice Referral Payouts");

        backoffice.MapPost("/referral/payouts/batch-payout", async (
            HttpRequest request,
            HttpContext ctx,
            ISender sender,
            DbContext dbContext) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);

            if (!request.HasFormContentType)
            {
                return Results.BadRequest(new { code = "PayoutBatch.InvalidContentType", message = "Requisição multipart/form-data esperada." });
            }

            var form = await request.ReadFormAsync();
            var receiptFile = form.Files.GetFile("receiptFile");
            if (receiptFile is null || receiptFile.Length == 0)
            {
                return Results.BadRequest(new { code = "PayoutReceipt.EmptyFile", message = "O arquivo de comprovante não possui conteúdo." });
            }

            var partnerIdStr = form["partnerParticipantId"].ToString();
            if (!Guid.TryParse(partnerIdStr, out var partnerParticipantId))
            {
                return Results.BadRequest(new { code = "PayoutBatch.PartnerRequired", message = "ID do parceiro comercial inválido." });
            }

            var transactionReference = form["transactionReference"].ToString();
            var ledgerIdsStr = form["ledgerIds"].ToString();

            IReadOnlyList<Guid> parsedLedgerIds;
            try
            {
                parsedLedgerIds = JsonSerializer.Deserialize<List<Guid>>(ledgerIdsStr) ?? new List<Guid>();
            }
            catch
            {
                parsedLedgerIds = ledgerIdsStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(id => Guid.TryParse(id, out var g) ? g : Guid.Empty)
                    .Where(g => g != Guid.Empty)
                    .ToList();
            }

            await using var stream = receiptFile.OpenReadStream();

            var command = new RegisterPartnerPayoutBatchCommand(
                partnerParticipantId,
                parsedLedgerIds,
                transactionReference,
                stream,
                receiptFile.FileName,
                receiptFile.ContentType,
                callerId
            );

            var result = await sender.Send(command);

            if (result.IsSuccess)
            {
                var audit = AuditLog.Create(
                    callerId,
                    callerEmail,
                    "Referral.PayoutBatchRegistered",
                    $"Partner/{partnerParticipantId}",
                    ip,
                    JsonSerializer.Serialize(new
                    {
                        PartnerParticipantId = partnerParticipantId,
                        CommissionsCount = result.Value,
                        TransactionReference = transactionReference,
                        ReceiptFileName = receiptFile.FileName
                    }));

                dbContext.Set<AuditLog>().Add(audit);
                await dbContext.SaveChangesAsync();
            }

            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.ReferralPayoutsManage))
          .DisableAntiforgery()
          .WithTags("Backoffice Referral Payouts");

        backoffice.MapPost("/referral/payouts/{id:guid}/cancel", async (
            Guid id,
            CancelCommissionLedgerEntryRequest req,
            HttpContext ctx,
            ISender sender,
            DbContext dbContext) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);
            var command = new CancelCommissionLedgerEntryCommand(id, req.Reason, callerId);
            var result = await sender.Send(command);

            if (result.IsSuccess)
            {
                var audit = AuditLog.Create(
                    callerId,
                    callerEmail,
                    "Referral.CommissionCancelled",
                    $"Ledger/{id}",
                    ip,
                    JsonSerializer.Serialize(new { LedgerId = id, Reason = req.Reason }));

                dbContext.Set<AuditLog>().Add(audit);
                await dbContext.SaveChangesAsync();
            }

            return EndpointResultExtensions.ToCommandHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.ReferralPayoutsManage)).WithTags("Backoffice Referral Payouts");

        backoffice.MapPost("/referral/payouts/run-enforcement", async (ISender sender) =>
        {
            var command = new ProcessReferralMaturityAndEnforcementCommand();
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.ReferralPayoutsManage)).WithTags("Backoffice Referral Payouts");

        backoffice.MapPost("/referral/reencrypt-contracts", async (IReferralDataReencryptionService reencryptionService) =>
        {
            var result = await reencryptionService.ReencryptPartnerContractsAsync();
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.ReferralPayoutsManage)).WithTags("Backoffice Referral Governance");

        // Discount Policies
        backoffice.MapGet("/referral/discount-policies", async (ISender sender) =>
        {
            var query = new GetPlanDiscountPoliciesQuery();
            var result = await sender.Send(query);
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.ReferralDiscountsRead)).WithTags("Backoffice Referral Discounts");

        backoffice.MapPut("/referral/discount-policies/{planCatalogId:guid}", async (
            Guid planCatalogId,
            ConfigurePlanDiscountPolicyRequest req,
            HttpContext ctx,
            ISender sender,
            DbContext dbContext) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);
            var command = new ConfigurePlanDiscountPolicyCommand(
                planCatalogId,
                req.DiscountType,
                req.DiscountValue,
                req.MaxDiscountCapPercentage);

            var result = await sender.Send(command);

            if (result.IsSuccess)
            {
                var audit = AuditLog.Create(
                    callerId,
                    callerEmail,
                    "Referral.PlanDiscountConfigured",
                    $"PlanDiscount/{planCatalogId}",
                    ip,
                    JsonSerializer.Serialize(new
                    {
                        PlanCatalogId = planCatalogId,
                        result.Value.PlanCode,
                        result.Value.DiscountType,
                        result.Value.DiscountValue,
                        result.Value.MaxDiscountCapPercentage
                    }));

                dbContext.Set<AuditLog>().Add(audit);
                await dbContext.SaveChangesAsync();
            }

            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.ReferralDiscountsManage)).WithTags("Backoffice Referral Discounts");

        backoffice.MapPost("/referral/discount-policies/{id:guid}/toggle-status", async (
            Guid id,
            HttpContext ctx,
            ISender sender,
            DbContext dbContext) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);
            var command = new TogglePlanDiscountPolicyStatusCommand(id);
            var result = await sender.Send(command);

            if (result.IsSuccess)
            {
                var audit = AuditLog.Create(
                    callerId,
                    callerEmail,
                    result.Value.IsActive ? "Referral.PlanDiscountActivated" : "Referral.PlanDiscountDeactivated",
                    $"PlanDiscount/{id}",
                    ip,
                    JsonSerializer.Serialize(new { PolicyId = id, result.Value.PlanCode, result.Value.IsActive }));

                dbContext.Set<AuditLog>().Add(audit);
                await dbContext.SaveChangesAsync();
            }

            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.ReferralDiscountsManage)).WithTags("Backoffice Referral Discounts");

        // Referral KPIs & Paid Media
        backoffice.MapGet("/referral/kpis", async (
            [Microsoft.AspNetCore.Mvc.FromQuery] DateTime? startDate,
            [Microsoft.AspNetCore.Mvc.FromQuery] DateTime? endDate,
            ISender sender) =>
        {
            var query = new GetReferralProgramKpisQuery(startDate, endDate);
            var result = await sender.Send(query);
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.ReferralAnalyticsRead)).WithTags("Backoffice Referral Analytics");

        backoffice.MapGet("/marketing/paid-media", async (
            [Microsoft.AspNetCore.Mvc.FromQuery] DateTime? startDate,
            [Microsoft.AspNetCore.Mvc.FromQuery] DateTime? endDate,
            ISender sender) =>
        {
            var query = new GetPaidMediaSpendsQuery(startDate, endDate);
            var result = await sender.Send(query);
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.ReferralAnalyticsRead)).WithTags("Backoffice Marketing Paid Media");

        backoffice.MapPost("/marketing/paid-media", async (
            CreatePaidMediaSpendRequest req,
            HttpContext ctx,
            ISender sender) =>
        {
            var (callerId, callerEmail, ip) = BackofficeHelpers.GetBackofficeActor(ctx);
            var command = new CreatePaidMediaSpendCommand(
                req.Channel,
                req.PeriodStartDateUtc,
                req.PeriodEndDateUtc,
                req.Amount,
                req.AcquisitionsCount,
                req.Notes,
                callerId,
                callerEmail,
                ip);
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result);
        }).RequireAuthorization(p => p.RequireClaim("Permission", BackofficePermissions.ReferralAnalyticsManage)).WithTags("Backoffice Marketing Paid Media");

        return backoffice;
    }
}

