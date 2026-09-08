using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Features.Approvals.Dtos;

namespace CriaCerto.Modules.Backoffice.Application.Features.Approvals;

public static class ApprovalMapper
{
    public static AdminApprovalRequestSummaryDto ToSummaryDto(AdminApprovalRequest entity) =>
        new(
            entity.Id,
            entity.RequestType,
            entity.Status,
            entity.Title,
            entity.TargetResourceId,
            entity.SupportTicketId,
            entity.RequestedByAdminUserId,
            entity.RequestedByAdminEmail,
            entity.RequestedAtUtc,
            entity.ExpiresAtUtc,
            entity.ReviewedByAdminUserId,
            entity.ReviewedByAdminEmail,
            entity.ReviewedAtUtc,
            entity.ExecutedAtUtc,
            !string.IsNullOrWhiteSpace(entity.DiffJson)
        );

    public static AdminApprovalRequestDetailDto ToDetailDto(AdminApprovalRequest entity) =>
        new(
            entity.Id,
            entity.RequestType,
            entity.Status,
            entity.Title,
            entity.Justification,
            entity.SupportTicketId,
            entity.TargetResourceId,
            entity.ImpactSummary,
            entity.PayloadJson,
            entity.DiffJson,
            entity.RequestedByAdminUserId,
            entity.RequestedByAdminEmail,
            entity.RequestedAtUtc,
            entity.ExpiresAtUtc,
            entity.ReviewedByAdminUserId,
            entity.ReviewedByAdminEmail,
            entity.ReviewedAtUtc,
            entity.ReviewNotes,
            entity.RejectionReason,
            entity.ExecutedAtUtc,
            entity.ExecutionResultJson,
            entity.ExecutionError
        );
}
