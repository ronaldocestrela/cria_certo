using System.Text.Json;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Enums;
using CriaCerto.Modules.Backoffice.Application.Features.DataRetention.Commands;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Backoffice.Application.Features.DataRetention.Commands;

public sealed class PurgeExpiredAdminSessionsCommandHandler
    : IRequestHandler<PurgeExpiredAdminSessionsCommand, Result<BackofficePurgeSummaryDto>>
{
    private readonly DbContext _dbContext;

    public PurgeExpiredAdminSessionsCommandHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<BackofficePurgeSummaryDto>> Handle(
        PurgeExpiredAdminSessionsCommand request,
        CancellationToken cancellationToken)
    {
        var now = request.ReferenceDateUtc ?? DateTime.UtcNow;
        var cutoff = now.AddDays(-Math.Max(0, request.SessionGraceDays));

        // Elegíveis: sessões revogadas anteriores ao cutoff OU refresh tokens expirados anteriores ao cutoff
        var staleSessions = await _dbContext.Set<AdminSession>()
            .Where(s =>
                (s.IsRevoked && s.ExpiresAtUtc < cutoff) ||
                (s.RefreshTokenExpiresAtUtc < cutoff))
            .ToListAsync(cancellationToken);

        int totalEvaluated = staleSessions.Count;
        int revokedPurged = staleSessions.Count(s => s.IsRevoked);
        int expiredPurged = staleSessions.Count(s => !s.IsRevoked);

        if (!request.DryRun && totalEvaluated > 0)
        {
            _dbContext.Set<AdminSession>().RemoveRange(staleSessions);

            var previousHash = await _dbContext.Set<AuditLog>()
                .AsNoTracking()
                .OrderByDescending(a => a.TimestampUtc)
                .Select(a => a.RecordHash)
                .FirstOrDefaultAsync(cancellationToken);

            var detailsJson = JsonSerializer.Serialize(new
            {
                EvaluatedSessions = totalEvaluated,
                PurgedRevokedSessions = revokedPurged,
                PurgedExpiredSessions = expiredPurged,
                TotalPurged = totalEvaluated,
                request.SessionGraceDays,
                CutoffUtc = cutoff,
                Action = "ADMIN_SESSIONS_PURGED",
                Specification = "LGPD Art. 16 & Art. 46"
            });

            var audit = AuditLog.CreateForensic(
                adminUserId: Guid.Empty,
                adminUserEmail: "system@criacerto.internal",
                actorRole: "SystemWorker",
                action: "ADMIN_SESSIONS_PURGED",
                category: AuditCategory.Compliance,
                severity: AuditSeverity.Low,
                resource: "Backoffice/AdminSessions",
                targetTenantId: null,
                targetTenantName: null,
                ipAddress: "127.0.0.1",
                userAgent: "DataRetentionPurgingWorker/1.0",
                previousRecordHash: previousHash,
                detailsJson: detailsJson);

            _dbContext.Set<AuditLog>().Add(audit);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var summary = new BackofficePurgeSummaryDto(
            EvaluatedSessions: totalEvaluated,
            PurgedRevokedSessions: revokedPurged,
            PurgedExpiredSessions: expiredPurged,
            TotalPurged: totalEvaluated,
            DryRun: request.DryRun,
            ExecutedAtUtc: DateTime.UtcNow
        );

        return Result.Success(summary);
    }
}
