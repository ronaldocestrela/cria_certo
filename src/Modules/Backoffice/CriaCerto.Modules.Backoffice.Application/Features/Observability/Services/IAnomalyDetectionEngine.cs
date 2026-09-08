using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Enums;

namespace CriaCerto.Modules.Backoffice.Application.Features.Observability.Services;

public interface IAnomalyDetectionEngine
{
    Task<Result<BackofficeAlert?>> EvaluatePolicyViolationsAsync(
        string actorOrIp,
        int recentFailureCount,
        string? contextJson = null,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default);

    Task<Result<BackofficeAlert?>> EvaluateCriticalActionTimeAsync(
        string action,
        AuditSeverity auditSeverity,
        Guid adminUserId,
        string adminEmail,
        DateTime timestampUtc,
        string? contextJson = null,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default);

    Task<Result<BackofficeAlert?>> EvaluateImpersonationBurstAsync(
        Guid adminUserId,
        string adminEmail,
        int recentSessionCount,
        string? contextJson = null,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default);

    Task<Result<BackofficeAlert?>> EvaluateAuditIntegrityAsync(
        bool isChainValid,
        int corruptedRecordsCount,
        string details,
        CancellationToken cancellationToken = default);
}
