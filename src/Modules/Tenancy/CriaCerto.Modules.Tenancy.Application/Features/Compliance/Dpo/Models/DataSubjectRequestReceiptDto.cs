namespace CriaCerto.Modules.Tenancy.Application.Features.Compliance.Dpo.Models;

/// <summary>
/// Comprovante formal e recibo emitido ao titular de dados com protocolo de atendimento LGPD.
/// </summary>
public record DataSubjectRequestReceiptDto(
    string ProtocolNumber,
    string DataSubjectName,
    string DataSubjectEmail,
    DataSubjectRole RelationshipType,
    DataSubjectRequestType RequestType,
    DateTime SubmittedAtUtc,
    DateTime MaxResponseDateUtc,
    int EstimatedBusinessDays,
    string DpoEmail,
    string ConfirmationMessage
);
