using System.Text.Json;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Enums;
using CriaCerto.Modules.Backoffice.Application.Features.Audit.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Backoffice.Application.Features.Audit.Commands;

public record ApplyAuditRetentionPolicyCommand(
    Guid ExecutedByAdminUserId,
    string ExecutedByAdminEmail,
    string IpAddress,
    bool DryRun = false,
    int CriticalRetentionDays = 1825,
    int HighRetentionDays = 1095,
    int MediumRetentionDays = 365,
    int LowRetentionDays = 90) : IRequest<Result<AuditRetentionExecutionResultDto>>;

public class ApplyAuditRetentionPolicyCommandHandler : IRequestHandler<ApplyAuditRetentionPolicyCommand, Result<AuditRetentionExecutionResultDto>>
{
    private readonly DbContext _dbContext;

    public ApplyAuditRetentionPolicyCommandHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<AuditRetentionExecutionResultDto>> Handle(ApplyAuditRetentionPolicyCommand request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var criticalThreshold = now.AddDays(-Math.Max(request.CriticalRetentionDays, 365));
        var highThreshold = now.AddDays(-Math.Max(request.HighRetentionDays, 180));
        var mediumThreshold = now.AddDays(-Math.Max(request.MediumRetentionDays, 90));
        var lowThreshold = now.AddDays(-Math.Max(request.LowRetentionDays, 30));

        // Purge candidates: only Low severity exceeding LowRetentionDays
        var purgeCandidates = await _dbContext.Set<AuditLog>()
            .Where(a => a.Severity == AuditSeverity.Low && a.TimestampUtc < lowThreshold)
            .ToListAsync(cancellationToken);

        // Archive candidates: Medium, High or Critical exceeding their retention periods and not yet archived
        var archiveCandidates = await _dbContext.Set<AuditLog>()
            .Where(a => !a.IsArchived && (
                (a.Severity == AuditSeverity.Medium && a.TimestampUtc < mediumThreshold) ||
                (a.Severity == AuditSeverity.High && a.TimestampUtc < highThreshold) ||
                (a.Severity == AuditSeverity.Critical && a.TimestampUtc < criticalThreshold)))
            .ToListAsync(cancellationToken);

        int purgedCount = purgeCandidates.Count;
        int archivedCount = archiveCandidates.Count;
        int totalEvaluated = purgedCount + archivedCount;

        string summary = request.DryRun
            ? $"Simulação (DryRun): {totalEvaluated} registros avaliados. {purgedCount} elegíveis para expurgo e {archivedCount} para arquivamento."
            : $"Política de retenção aplicada com sucesso: {purgedCount} registros purgados e {archivedCount} arquivados.";

        if (!request.DryRun && totalEvaluated > 0)
        {
            if (purgedCount > 0)
            {
                _dbContext.Set<AuditLog>().RemoveRange(purgeCandidates);
            }

            foreach (var toArchive in archiveCandidates)
            {
                toArchive.MarkAsArchived();
            }

            // Register forensic audit of retention execution
            var retentionAudit = AuditLog.CreateForensic(
                request.ExecutedByAdminUserId,
                request.ExecutedByAdminEmail,
                "PlatformOwner",
                "Audit.RetentionPolicyApplied",
                AuditCategory.Governance,
                AuditSeverity.Critical,
                "AuditLogs/RetentionPolicy",
                null,
                null,
                request.IpAddress,
                null,
                null,
                null,
                null,
                JsonSerializer.Serialize(new
                {
                    PurgedCount = purgedCount,
                    ArchivedCount = archivedCount,
                    request.CriticalRetentionDays,
                    request.HighRetentionDays,
                    request.MediumRetentionDays,
                    request.LowRetentionDays
                }));

            _dbContext.Set<AuditLog>().Add(retentionAudit);

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(new AuditRetentionExecutionResultDto(
            totalEvaluated,
            archivedCount,
            purgedCount,
            request.DryRun,
            summary,
            DateTime.UtcNow));
    }
}
