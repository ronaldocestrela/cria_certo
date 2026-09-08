using CriaCerto.Modules.Backoffice.Application.Domain.Entities;

namespace CriaCerto.Modules.Backoffice.Application.Features.Approvals.Dtos;

public record AdminApprovalRequestSummaryDto(
    Guid Id,
    ApprovalRequestType RequestType,
    ApprovalRequestStatus Status,
    string Title,
    string TargetResourceId,
    string? SupportTicketId,
    Guid RequestedByAdminUserId,
    string RequestedByAdminEmail,
    DateTime RequestedAtUtc,
    DateTime ExpiresAtUtc,
    Guid? ReviewedByAdminUserId,
    string? ReviewedByAdminEmail,
    DateTime? ReviewedAtUtc,
    DateTime? ExecutedAtUtc,
    bool HasDiff
);

public record AdminApprovalRequestDetailDto(
    Guid Id,
    ApprovalRequestType RequestType,
    ApprovalRequestStatus Status,
    string Title,
    string Justification,
    string? SupportTicketId,
    string TargetResourceId,
    string ImpactSummary,
    string PayloadJson,
    string? DiffJson,
    Guid RequestedByAdminUserId,
    string RequestedByAdminEmail,
    DateTime RequestedAtUtc,
    DateTime ExpiresAtUtc,
    Guid? ReviewedByAdminUserId,
    string? ReviewedByAdminEmail,
    DateTime? ReviewedAtUtc,
    string? ReviewNotes,
    string? RejectionReason,
    DateTime? ExecutedAtUtc,
    string? ExecutionResultJson,
    string? ExecutionError
);

public record CreateApprovalRequestRequest(
    ApprovalRequestType RequestType,
    string Title,
    string Justification,
    string TargetResourceId,
    string ImpactSummary,
    string PayloadJson,
    string? SupportTicketId = null,
    string? DiffJson = null,
    int? ExpirationHours = null
);

public record ApproveApprovalRequestRequest(
    string? ReviewNotes = null
);

public record RejectApprovalRequestRequest(
    string RejectionReason
);

public record CancelApprovalRequestRequest(
    string? CancelReason = null
);

public record PendingApprovalsCountDto(
    int TotalPending,
    int MyPendingRequests,
    int PendingReviewForMe
);

public record PagedApprovalResult(
    IReadOnlyCollection<AdminApprovalRequestSummaryDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);
