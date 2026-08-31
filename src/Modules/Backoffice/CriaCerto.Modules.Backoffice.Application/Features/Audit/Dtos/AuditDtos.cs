using CriaCerto.Modules.Backoffice.Application.Domain.Enums;

namespace CriaCerto.Modules.Backoffice.Application.Features.Audit.Dtos;

public record AuditLogSummaryDto(
    Guid Id,
    DateTime TimestampUtc,
    Guid AdminUserId,
    string AdminUserEmail,
    string? ActorRole,
    string Action,
    AuditCategory Category,
    AuditSeverity Severity,
    string Resource,
    Guid? TargetTenantId,
    string? TargetTenantName,
    string IpAddress,
    string RecordHash,
    bool IsIntegrityValid,
    bool IsArchived);

public record AuditLogDetailDto(
    Guid Id,
    DateTime TimestampUtc,
    Guid AdminUserId,
    string AdminUserEmail,
    string? ActorRole,
    string Action,
    AuditCategory Category,
    AuditSeverity Severity,
    string Resource,
    Guid? TargetTenantId,
    string? TargetTenantName,
    string IpAddress,
    string? UserAgent,
    string? OldValuesJson,
    string? NewValuesJson,
    string? DetailsJson,
    string RecordHash,
    string? PreviousRecordHash,
    bool IsIntegrityValid,
    bool IsArchived);

public record PagedAuditLogsDto(
    IReadOnlyList<AuditLogSummaryDto> Items,
    int TotalCount,
    int PageNumber,
    int PageSize,
    int TotalPages);

public record AuditStatsDto(
    int TotalLogs,
    int LogsLast24Hours,
    int CriticalEventsCount,
    int TamperedEventsCount,
    Dictionary<string, int> CountByCategory,
    Dictionary<string, int> CountBySeverity,
    bool IsChainIntegrityValid);

public record AuditTrailVerificationResultDto(
    bool IsChainValid,
    int TotalRecordsChecked,
    int ValidRecordsCount,
    int TamperedRecordsCount,
    Guid? FirstTamperedRecordId,
    string? Message,
    DateTime CheckedAtUtc);

public record AuditRetentionExecutionResultDto(
    int TotalEvaluated,
    int ArchivedCount,
    int PurgedCount,
    bool IsDryRun,
    string Summary,
    DateTime ExecutedAtUtc);

public record AuditExportFileDto(
    string FileName,
    string ContentType,
    byte[] Content);

public record ApplyAuditRetentionRequest(
    bool DryRun = false,
    int CriticalRetentionDays = 1825,
    int HighRetentionDays = 1095,
    int MediumRetentionDays = 365,
    int LowRetentionDays = 90);

