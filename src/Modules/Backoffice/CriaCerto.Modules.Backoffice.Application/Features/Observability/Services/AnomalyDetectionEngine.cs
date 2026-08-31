using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Enums;
using CriaCerto.Modules.Backoffice.Application.Telemetry;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Backoffice.Application.Features.Observability.Services;

public class AnomalyDetectionEngine : IAnomalyDetectionEngine
{
    private readonly DbContext _dbContext;
    public const int DefaultPolicyFailureThreshold = 5;
    public const int DefaultImpersonationBurstThreshold = 3;

    public AnomalyDetectionEngine(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<BackofficeAlert?>> EvaluatePolicyViolationsAsync(
        string actorOrIp,
        int recentFailureCount,
        string? contextJson = null,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        if (recentFailureCount < DefaultPolicyFailureThreshold)
        {
            return Result.Success<BackofficeAlert?>(null);
        }

        var fingerprint = $"{BackofficeAlertRules.PolicyBruteForce}:{actorOrIp.Trim().ToLowerInvariant()}";
        var severity = recentFailureCount >= 10 ? AlertSeverity.Critical : AlertSeverity.Warning;
        var description = $"Detectadas {recentFailureCount} falhas consecutivas de autenticação ou autorização para o alvo '{actorOrIp}'. Possível varredura de permissões ou ataque de força bruta.";

        return await CreateOrIncrementAlertAsync(
            ruleCode: BackofficeAlertRules.PolicyBruteForce,
            title: BackofficeAlertRules.GetDefaultTitle(BackofficeAlertRules.PolicyBruteForce),
            description: description,
            severity: severity,
            fingerprint: fingerprint,
            contextJson: contextJson ?? $"{{\"target\":\"{actorOrIp}\",\"failureCount\":{recentFailureCount}}}",
            targetTenantId: tenantId,
            cancellationToken: cancellationToken);
    }

    public async Task<Result<BackofficeAlert?>> EvaluateCriticalActionTimeAsync(
        string action,
        AuditSeverity auditSeverity,
        Guid adminUserId,
        string adminEmail,
        DateTime timestampUtc,
        string? contextJson = null,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        // Only trigger for High or Critical actions
        if (auditSeverity != AuditSeverity.Critical && auditSeverity != AuditSeverity.High)
        {
            return Result.Success<BackofficeAlert?>(null);
        }

        // Convert to Brasilia Time (UTC-3)
        var brtTime = timestampUtc.AddHours(-3);
        var isWeekend = brtTime.DayOfWeek == DayOfWeek.Saturday || brtTime.DayOfWeek == DayOfWeek.Sunday;
        var isOffHours = brtTime.Hour < 6 || brtTime.Hour >= 22;

        if (!isWeekend && !isOffHours)
        {
            return Result.Success<BackofficeAlert?>(null);
        }

        var fingerprint = $"{BackofficeAlertRules.OffHoursCriticalAction}:{adminUserId}:{action.Trim()}";
        var reason = isWeekend ? "durante o final de semana" : $"no horário noturno ({brtTime:HH:mm} BRT)";
        var description = $"Ação crítica '{action}' ({auditSeverity}) executada por '{adminEmail}' {reason} sem pré-autorização de emergência.";

        return await CreateOrIncrementAlertAsync(
            ruleCode: BackofficeAlertRules.OffHoursCriticalAction,
            title: BackofficeAlertRules.GetDefaultTitle(BackofficeAlertRules.OffHoursCriticalAction),
            description: description,
            severity: AlertSeverity.Warning,
            fingerprint: fingerprint,
            contextJson: contextJson ?? $"{{\"action\":\"{action}\",\"executedAtBrt\":\"{brtTime:s}\",\"actor\":\"{adminEmail}\"}}",
            targetTenantId: tenantId,
            relatedAdminUserId: adminUserId,
            relatedAdminEmail: adminEmail,
            cancellationToken: cancellationToken);
    }

    public async Task<Result<BackofficeAlert?>> EvaluateImpersonationBurstAsync(
        Guid adminUserId,
        string adminEmail,
        int recentSessionCount,
        string? contextJson = null,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        if (recentSessionCount < DefaultImpersonationBurstThreshold)
        {
            return Result.Success<BackofficeAlert?>(null);
        }

        var fingerprint = $"{BackofficeAlertRules.ImpersonationBurst}:{adminUserId}";
        var description = $"Operador '{adminEmail}' iniciou {recentSessionCount} sessões de impersonação em uma janela curta de tempo. Comportamento anômalo acima do limite operacional.";

        return await CreateOrIncrementAlertAsync(
            ruleCode: BackofficeAlertRules.ImpersonationBurst,
            title: BackofficeAlertRules.GetDefaultTitle(BackofficeAlertRules.ImpersonationBurst),
            description: description,
            severity: AlertSeverity.Warning,
            fingerprint: fingerprint,
            contextJson: contextJson ?? $"{{\"adminUserId\":\"{adminUserId}\",\"sessionCount\":{recentSessionCount}}}",
            targetTenantId: tenantId,
            relatedAdminUserId: adminUserId,
            relatedAdminEmail: adminEmail,
            cancellationToken: cancellationToken);
    }

    public async Task<Result<BackofficeAlert?>> EvaluateAuditIntegrityAsync(
        bool isChainValid,
        int corruptedRecordsCount,
        string details,
        CancellationToken cancellationToken = default)
    {
        if (isChainValid && corruptedRecordsCount == 0)
        {
            return Result.Success<BackofficeAlert?>(null);
        }

        var fingerprint = $"{BackofficeAlertRules.ForensicTamperDetected}:integrity_corruption";
        var description = $"Detectada quebra da cadeia criptográfica na trilha de auditoria forense ({corruptedRecordsCount} registros inconsistentes). Detalhes: {details}";

        return await CreateOrIncrementAlertAsync(
            ruleCode: BackofficeAlertRules.ForensicTamperDetected,
            title: BackofficeAlertRules.GetDefaultTitle(BackofficeAlertRules.ForensicTamperDetected),
            description: description,
            severity: AlertSeverity.Critical,
            fingerprint: fingerprint,
            contextJson: $"{{\"corruptedCount\":{corruptedRecordsCount},\"details\":\"{details}\"}}",
            cancellationToken: cancellationToken);
    }

    private async Task<Result<BackofficeAlert?>> CreateOrIncrementAlertAsync(
        string ruleCode,
        string title,
        string description,
        AlertSeverity severity,
        string fingerprint,
        string contextJson,
        Guid? targetTenantId = null,
        string? targetTenantName = null,
        Guid? relatedAdminUserId = null,
        string? relatedAdminEmail = null,
        CancellationToken cancellationToken = default)
    {
        var existingAlert = await _dbContext.Set<BackofficeAlert>()
            .FirstOrDefaultAsync(a => a.Fingerprint == fingerprint && a.Status != AlertStatus.Resolved, cancellationToken);

        if (existingAlert != null)
        {
            var incrementResult = existingAlert.IncrementOccurrence(contextJson);
            if (incrementResult.IsFailure)
                return Result.Failure<BackofficeAlert?>(incrementResult.Error);

            await _dbContext.SaveChangesAsync(cancellationToken);
            BackofficeTelemetry.RecordAlert(ruleCode, severity.ToString());
            return Result.Success<BackofficeAlert?>(existingAlert);
        }

        var createResult = BackofficeAlert.Create(
            ruleCode: ruleCode,
            title: title,
            description: description,
            severity: severity,
            fingerprint: fingerprint,
            contextJson: contextJson,
            targetTenantId: targetTenantId,
            targetTenantName: targetTenantName,
            relatedAdminUserId: relatedAdminUserId,
            relatedAdminEmail: relatedAdminEmail);

        if (createResult.IsFailure)
            return Result.Failure<BackofficeAlert?>(createResult.Error);

        var alert = createResult.Value;
        await _dbContext.Set<BackofficeAlert>().AddAsync(alert, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        BackofficeTelemetry.RecordAlert(ruleCode, severity.ToString());

        return Result.Success<BackofficeAlert?>(alert);
    }
}
