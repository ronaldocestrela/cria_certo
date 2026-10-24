namespace CriaCerto.Modules.Referral.Application.Abstractions;

public interface IReferralAuditSink
{
    /// <summary>
    /// Registra uma entrada de auditoria imutável (sistema/forense) para ações automáticas do módulo Referral.
    /// </summary>
    Task RecordSystemAuditAsync(
        string action,
        string resource,
        Guid? targetTenantId,
        string detailsJson,
        CancellationToken cancellationToken = default);
}
