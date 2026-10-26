namespace CriaCerto.Modules.Tenancy.Application.Abstractions;

public interface ITenancyAuditSink
{
    /// <summary>
    /// Registra uma entrada de auditoria imutável e encadeada por hash forense SHA-256
    /// para comprovação do aceite dos termos e políticas de privacidade sob a LGPD (Art. 8º).
    /// </summary>
    Task RecordUserConsentAuditAsync(
        Guid userId,
        string userEmail,
        string termsVersion,
        string privacyPolicyVersion,
        string ipAddress,
        CancellationToken cancellationToken = default);
}
