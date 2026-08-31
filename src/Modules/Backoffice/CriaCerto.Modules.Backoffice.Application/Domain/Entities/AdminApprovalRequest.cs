using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;

namespace CriaCerto.Modules.Backoffice.Application.Domain.Entities;

public enum ApprovalRequestType
{
    PublishPlanVersion,
    MassTenantSuspension,
    ExtendedAccessGrant,
    CustomCriticalAction
}

public enum ApprovalRequestStatus
{
    Pending,
    Approved,
    Rejected,
    Executed,
    Cancelled,
    Expired
}

public class AdminApprovalRequest
{
    public const int DefaultExpirationHours = 48;
    public const int MinExpirationHours = 1;
    public const int MaxExpirationHours = 168; // 7 dias

    public Guid Id { get; private set; }
    public ApprovalRequestType RequestType { get; private set; }
    public ApprovalRequestStatus Status { get; private set; }
    public string Title { get; private set; } = default!;
    public string Justification { get; private set; } = default!;
    public string? SupportTicketId { get; private set; }
    public string TargetResourceId { get; private set; } = default!;
    public string ImpactSummary { get; private set; } = default!;
    public string PayloadJson { get; private set; } = default!;
    public string? DiffJson { get; private set; }

    public Guid RequestedByAdminUserId { get; private set; }
    public string RequestedByAdminEmail { get; private set; } = default!;
    public DateTime RequestedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }

    public Guid? ReviewedByAdminUserId { get; private set; }
    public string? ReviewedByAdminEmail { get; private set; }
    public DateTime? ReviewedAtUtc { get; private set; }
    public string? ReviewNotes { get; private set; }
    public string? RejectionReason { get; private set; }

    public DateTime? ExecutedAtUtc { get; private set; }
    public string? ExecutionResultJson { get; private set; }
    public string? ExecutionError { get; private set; }

    private AdminApprovalRequest() { }

    public static Result<AdminApprovalRequest> Create(
        ApprovalRequestType requestType,
        string title,
        string justification,
        string targetResourceId,
        string impactSummary,
        string payloadJson,
        Guid requestedByAdminUserId,
        string requestedByAdminEmail,
        string? supportTicketId = null,
        string? diffJson = null,
        int expirationHours = DefaultExpirationHours)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Result.Failure<AdminApprovalRequest>(ApprovalErrors.TitleRequired);

        if (string.IsNullOrWhiteSpace(justification) || justification.Trim().Length < 10)
            return Result.Failure<AdminApprovalRequest>(ApprovalErrors.JustificationRequired);

        if (string.IsNullOrWhiteSpace(targetResourceId))
            return Result.Failure<AdminApprovalRequest>(ApprovalErrors.TargetResourceRequired);

        if (string.IsNullOrWhiteSpace(impactSummary))
            return Result.Failure<AdminApprovalRequest>(ApprovalErrors.ImpactSummaryRequired);

        if (string.IsNullOrWhiteSpace(payloadJson))
            return Result.Failure<AdminApprovalRequest>(ApprovalErrors.PayloadRequired);

        var clampedHours = Math.Clamp(expirationHours, MinExpirationHours, MaxExpirationHours);
        var now = DateTime.UtcNow;

        var request = new AdminApprovalRequest
        {
            Id = Guid.NewGuid(),
            RequestType = requestType,
            Status = ApprovalRequestStatus.Pending,
            Title = title.Trim(),
            Justification = justification.Trim(),
            SupportTicketId = string.IsNullOrWhiteSpace(supportTicketId) ? null : supportTicketId.Trim().ToUpperInvariant(),
            TargetResourceId = targetResourceId.Trim(),
            ImpactSummary = impactSummary.Trim(),
            PayloadJson = payloadJson,
            DiffJson = diffJson,
            RequestedByAdminUserId = requestedByAdminUserId,
            RequestedByAdminEmail = requestedByAdminEmail,
            RequestedAtUtc = now,
            ExpiresAtUtc = now.AddHours(clampedHours)
        };

        return Result.Success(request);
    }

    public bool IsExpired() => DateTime.UtcNow >= ExpiresAtUtc;

    public void CheckAndMarkExpired()
    {
        if (Status == ApprovalRequestStatus.Pending && IsExpired())
        {
            Status = ApprovalRequestStatus.Expired;
        }
    }

    public Result Approve(Guid reviewerId, string reviewerEmail, string? reviewNotes = null)
    {
        CheckAndMarkExpired();

        if (Status == ApprovalRequestStatus.Expired)
            return Result.Failure(ApprovalErrors.Expired);

        if (Status != ApprovalRequestStatus.Pending)
            return Result.Failure(ApprovalErrors.AlreadyDecided);

        // 4-Eyes Principle: Requester cannot self-approve
        if (reviewerId == RequestedByAdminUserId)
            return Result.Failure(ApprovalErrors.CannotSelfApprove);

        Status = ApprovalRequestStatus.Approved;
        ReviewedByAdminUserId = reviewerId;
        ReviewedByAdminEmail = reviewerEmail;
        ReviewedAtUtc = DateTime.UtcNow;
        ReviewNotes = reviewNotes?.Trim();

        return Result.Success();
    }

    public Result Reject(Guid reviewerId, string reviewerEmail, string rejectionReason)
    {
        CheckAndMarkExpired();

        if (Status == ApprovalRequestStatus.Expired)
            return Result.Failure(ApprovalErrors.Expired);

        if (Status != ApprovalRequestStatus.Pending)
            return Result.Failure(ApprovalErrors.AlreadyDecided);

        // 4-Eyes Principle: Requester cannot decide own request
        if (reviewerId == RequestedByAdminUserId)
            return Result.Failure(ApprovalErrors.CannotSelfApprove);

        if (string.IsNullOrWhiteSpace(rejectionReason) || rejectionReason.Trim().Length < 5)
            return Result.Failure(ApprovalErrors.RejectionReasonRequired);

        Status = ApprovalRequestStatus.Rejected;
        ReviewedByAdminUserId = reviewerId;
        ReviewedByAdminEmail = reviewerEmail;
        ReviewedAtUtc = DateTime.UtcNow;
        RejectionReason = rejectionReason.Trim();

        return Result.Success();
    }

    public Result Cancel(Guid requesterId, string? cancelReason = null)
    {
        if (requesterId != RequestedByAdminUserId)
            return Result.Failure(ApprovalErrors.OnlyRequesterCanCancel);

        if (Status != ApprovalRequestStatus.Pending)
            return Result.Failure(ApprovalErrors.AlreadyDecided);

        Status = ApprovalRequestStatus.Cancelled;
        ReviewNotes = cancelReason?.Trim();

        return Result.Success();
    }

    public Result MarkAsExecuted(string? resultJson = null)
    {
        if (Status != ApprovalRequestStatus.Approved)
            return Result.Failure(ApprovalErrors.CannotExecuteUnapproved);

        Status = ApprovalRequestStatus.Executed;
        ExecutedAtUtc = DateTime.UtcNow;
        ExecutionResultJson = resultJson;
        ExecutionError = null;

        return Result.Success();
    }

    public void MarkAsExecutionFailed(string error)
    {
        ExecutionError = error;
    }
}
