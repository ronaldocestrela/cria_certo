using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Enums;
using CriaCerto.Modules.Backoffice.Infrastructure.Persistence;
using CriaCerto.Modules.Referral.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CriaCerto.Api.Services;

public sealed class ReferralBackofficeAuditSink : IReferralAuditSink
{
    private static readonly Guid SystemJobUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private const string SystemJobEmail = "system.referral@criacerto.internal";

    private readonly BackofficeDbContext _dbContext;
    private readonly ILogger<ReferralBackofficeAuditSink> _logger;

    public ReferralBackofficeAuditSink(
        BackofficeDbContext dbContext,
        ILogger<ReferralBackofficeAuditSink> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task RecordSystemAuditAsync(
        string action,
        string resource,
        Guid? targetTenantId,
        string detailsJson,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var previousHash = await _dbContext.AuditLogs
                .AsNoTracking()
                .OrderByDescending(a => a.TimestampUtc)
                .Select(a => a.RecordHash)
                .FirstOrDefaultAsync(cancellationToken);

            var audit = AuditLog.CreateForensic(
                adminUserId: SystemJobUserId,
                adminUserEmail: SystemJobEmail,
                actorRole: "SystemBackgroundJob",
                action: action,
                category: AuditCategory.Billing,
                severity: AuditSeverity.High,
                resource: resource,
                targetTenantId: targetTenantId,
                targetTenantName: null,
                ipAddress: "127.0.0.1",
                userAgent: "ReferralMaturityAndEnforcementJob/1.0",
                previousRecordHash: previousHash,
                detailsJson: detailsJson);

            _dbContext.AuditLogs.Add(audit);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Auditoria forense registrada para ação {Action} no recurso {Resource} (Hash: {Hash}).",
                action, resource, audit.RecordHash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao registrar auditoria forense para ação {Action} no recurso {Resource}.", action, resource);
        }
    }
}
