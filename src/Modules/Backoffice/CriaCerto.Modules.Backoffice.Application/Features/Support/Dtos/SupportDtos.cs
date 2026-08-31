namespace CriaCerto.Modules.Backoffice.Application.Features.Support.Dtos;

public enum RemediationActionType
{
    RequestClientCacheReset = 1,
    EvictTenantCache = 2,
    ReconcileEntitlements = 3,
    RetryFailedQueueItems = 4,
    ResetTransientLocks = 5
}

public sealed record TenantOverviewDto(
    Guid Id,
    string LegalName,
    string TradeName,
    string DocumentNumber,
    string Status,
    string SubscribedPlan,
    bool IsProtected,
    string? SizeSegment,
    string? CommercialRegion,
    string? ProductiveProfile,
    string? ChurnRisk,
    DateTime CreatedAtUtc
);

public sealed record SyncHealthDto(
    string Status, // "Healthy", "Warning", "Critical"
    int PendingQueueOperations,
    int RecentConflictsCount,
    DateTime? LastSuccessfulSyncUtc,
    string HealthSummary
);

public sealed record ModuleEntitlementDto(
    string ModuleName,
    bool IsEnabled,
    int HeadLimit,
    int CurrentHeadCount,
    bool OverCapacity,
    string Notes
);

public sealed record QueueHealthDto(
    int ActiveJobsCount,
    int PendingMessagesCount,
    int FailedMessagesCount,
    string Status, // "Idle", "Processing", "Degraded"
    DateTime? LastRunUtc
);

public sealed record RecentFailureDto(
    string ErrorCode,
    string Message,
    string EndpointOrSource,
    string Severity, // "Warning", "Error", "Critical"
    DateTime TimestampUtc
);

public sealed record ActiveSupportSessionDto(
    Guid SessionId,
    Guid AdminUserId,
    string AdminEmail,
    string SupportTicket,
    DateTime StartedAtUtc,
    DateTime ExpiresAtUtc,
    int RemainingMinutes
);

public sealed record TenantDiagnosticReportDto(
    TenantOverviewDto Overview,
    SyncHealthDto SyncHealth,
    IReadOnlyCollection<ModuleEntitlementDto> Modules,
    QueueHealthDto QueueHealth,
    IReadOnlyCollection<RecentFailureDto> RecentFailures,
    ActiveSupportSessionDto? ActiveSupportSession,
    DateTime GeneratedAtUtc
);

public sealed record PlaybookStepDto(
    int Order,
    string Action,
    string ExpectedResult,
    bool IsRequired
);

public sealed record SupportPlaybookDto(
    string Id,
    string Code,
    string Title,
    string Category,
    string Description,
    string RecommendedActionType,
    IReadOnlyCollection<PlaybookStepDto> Steps
);

public sealed record ExecuteRemediationRequest(
    string ActionType,
    string SupportTicketId,
    string Justification
);

public sealed record RemediationExecutionResultDto(
    Guid ExecutionId,
    Guid TenantId,
    string ActionType,
    string Status,
    DateTime ExecutedAtUtc,
    string Message,
    string SupportTicketId,
    string OperatorEmail
);
