using CriaCerto.Modules.Backoffice.Application.Domain.Enums;

namespace CriaCerto.Modules.Backoffice.Application.Features.Observability.Dtos;

public record BackofficeAlertDto(
    Guid Id,
    string RuleCode,
    string Title,
    string Description,
    AlertSeverity Severity,
    AlertStatus Status,
    string Fingerprint,
    int OccurrenceCount,
    DateTime FirstTriggeredAtUtc,
    DateTime LastTriggeredAtUtc,
    string ContextJson,
    Guid? TargetTenantId,
    string? TargetTenantName,
    Guid? RelatedAdminUserId,
    string? RelatedAdminEmail,
    DateTime? AcknowledgedAtUtc,
    string? AcknowledgedByEmail,
    DateTime? ResolvedAtUtc,
    string? ResolvedByEmail,
    string? ResolutionNotes);

public record PagedAlertsDto(
    IReadOnlyList<BackofficeAlertDto> Items,
    int TotalCount,
    int PageNumber,
    int PageSize,
    int TotalPages);

public record OperationalHealthDto(
    OperationalHealthStatus Status,
    string StatusSummary,
    int ActiveCriticalAlerts,
    int ActiveWarningAlerts,
    int ActiveInfoAlerts,
    int ActiveImpersonationsCount,
    int PolicyFailuresLast24Hours,
    bool IsAuditChainValid,
    double AverageOperationLatencyMs,
    DateTime EvaluatedAtUtc);

public record OperationMetricItemDto(
    string OperationName,
    int Invocations,
    double AverageDurationMs,
    double MaxDurationMs,
    int FailureCount);

public record BackofficeMetricsSummaryDto(
    int TotalActionsLast24Hours,
    int PolicyFailuresLast24Hours,
    int ActiveImpersonations,
    int TotalAlertsLast24Hours,
    IReadOnlyList<OperationMetricItemDto> TopOperations,
    DateTime GeneratedAtUtc);

public record AcknowledgeAlertRequest(
    Guid AdminUserId,
    string AdminEmail);

public record ResolveAlertRequest(
    Guid AdminUserId,
    string AdminEmail,
    string ResolutionNotes);

public record SimulateAlertRequest(
    string RuleCode,
    AlertSeverity Severity,
    string Title,
    string Description,
    string? ContextJson = null);
