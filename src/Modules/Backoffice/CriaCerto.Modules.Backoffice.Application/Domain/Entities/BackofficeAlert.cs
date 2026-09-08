using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Enums;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;

namespace CriaCerto.Modules.Backoffice.Application.Domain.Entities;

public class BackofficeAlert
{
    public Guid Id { get; private set; }
    public string RuleCode { get; private set; } = default!;
    public string Title { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public AlertSeverity Severity { get; private set; }
    public AlertStatus Status { get; private set; }
    public string Fingerprint { get; private set; } = default!;
    public int OccurrenceCount { get; private set; }
    public DateTime FirstTriggeredAtUtc { get; private set; }
    public DateTime LastTriggeredAtUtc { get; private set; }
    public string ContextJson { get; private set; } = default!;
    public Guid? TargetTenantId { get; private set; }
    public string? TargetTenantName { get; private set; }
    public Guid? RelatedAdminUserId { get; private set; }
    public string? RelatedAdminEmail { get; private set; }

    public DateTime? AcknowledgedAtUtc { get; private set; }
    public Guid? AcknowledgedByAdminUserId { get; private set; }
    public string? AcknowledgedByEmail { get; private set; }

    public DateTime? ResolvedAtUtc { get; private set; }
    public Guid? ResolvedByAdminUserId { get; private set; }
    public string? ResolvedByEmail { get; private set; }
    public string? ResolutionNotes { get; private set; }

    private BackofficeAlert() { }

    public static Result<BackofficeAlert> Create(
        string ruleCode,
        string title,
        string description,
        AlertSeverity severity,
        string fingerprint,
        string? contextJson = null,
        Guid? targetTenantId = null,
        string? targetTenantName = null,
        Guid? relatedAdminUserId = null,
        string? relatedAdminEmail = null,
        DateTime? triggeredAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(ruleCode))
            return Result.Failure<BackofficeAlert>(ObservabilityErrors.RuleCodeRequired);

        if (string.IsNullOrWhiteSpace(title))
            return Result.Failure<BackofficeAlert>(ObservabilityErrors.TitleRequired);

        var now = triggeredAtUtc ?? DateTime.UtcNow;
        var computedFingerprint = string.IsNullOrWhiteSpace(fingerprint)
            ? $"{ruleCode}:{targetTenantId}:{relatedAdminUserId}"
            : fingerprint.Trim();

        var alert = new BackofficeAlert
        {
            Id = Guid.NewGuid(),
            RuleCode = ruleCode.Trim(),
            Title = title.Trim(),
            Description = description?.Trim() ?? string.Empty,
            Severity = severity,
            Status = AlertStatus.Active,
            Fingerprint = computedFingerprint,
            OccurrenceCount = 1,
            FirstTriggeredAtUtc = now,
            LastTriggeredAtUtc = now,
            ContextJson = string.IsNullOrWhiteSpace(contextJson) ? "{}" : contextJson.Trim(),
            TargetTenantId = targetTenantId,
            TargetTenantName = targetTenantName?.Trim(),
            RelatedAdminUserId = relatedAdminUserId,
            RelatedAdminEmail = relatedAdminEmail?.Trim()
        };

        return Result.Success(alert);
    }

    public Result IncrementOccurrence(string? updatedContextJson = null, DateTime? triggeredAtUtc = null)
    {
        if (Status == AlertStatus.Resolved)
            return Result.Failure(ObservabilityErrors.AlreadyResolved);

        OccurrenceCount++;
        LastTriggeredAtUtc = triggeredAtUtc ?? DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(updatedContextJson))
        {
            ContextJson = updatedContextJson.Trim();
        }

        return Result.Success();
    }

    public Result Acknowledge(Guid adminUserId, string adminEmail, DateTime? acknowledgedAtUtc = null)
    {
        if (adminUserId == Guid.Empty || string.IsNullOrWhiteSpace(adminEmail))
            return Result.Failure(ObservabilityErrors.AdminRequired);

        if (Status == AlertStatus.Resolved)
            return Result.Failure(ObservabilityErrors.CannotAcknowledgeResolved);

        Status = AlertStatus.Acknowledged;
        AcknowledgedAtUtc = acknowledgedAtUtc ?? DateTime.UtcNow;
        AcknowledgedByAdminUserId = adminUserId;
        AcknowledgedByEmail = adminEmail.Trim();

        return Result.Success();
    }

    public Result Resolve(Guid adminUserId, string adminEmail, string resolutionNotes, DateTime? resolvedAtUtc = null)
    {
        if (adminUserId == Guid.Empty || string.IsNullOrWhiteSpace(adminEmail))
            return Result.Failure(ObservabilityErrors.AdminRequired);

        if (string.IsNullOrWhiteSpace(resolutionNotes) || resolutionNotes.Trim().Length < 5)
            return Result.Failure(ObservabilityErrors.ResolutionNotesRequired);

        if (Status == AlertStatus.Resolved)
            return Result.Failure(ObservabilityErrors.AlreadyResolved);

        Status = AlertStatus.Resolved;
        ResolvedAtUtc = resolvedAtUtc ?? DateTime.UtcNow;
        ResolvedByAdminUserId = adminUserId;
        ResolvedByEmail = adminEmail.Trim();
        ResolutionNotes = resolutionNotes.Trim();

        return Result.Success();
    }
}
