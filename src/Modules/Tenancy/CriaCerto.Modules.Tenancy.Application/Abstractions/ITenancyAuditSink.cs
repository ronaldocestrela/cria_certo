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

    /// <summary>
    /// Registra auditoria forense quando dados cadastrais do titular são retificados (Art. 18, III).
    /// </summary>
    Task RecordUserProfileUpdateAuditAsync(
        Guid userId,
        string userEmail,
        string oldFullName,
        string newFullName,
        string? oldPhone,
        string? newPhone,
        string ipAddress,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registra auditoria forense quando a senha do usuário é alterada.
    /// </summary>
    Task RecordUserPasswordChangeAuditAsync(
        Guid userId,
        string userEmail,
        string ipAddress,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registra auditoria forense quando o titular exerce o direito de portabilidade (Art. 18, II e V).
    /// </summary>
    Task RecordDataSubjectPortabilityExportAuditAsync(
        Guid userId,
        string userEmail,
        string ipAddress,
        CancellationToken cancellationToken = default);
}
