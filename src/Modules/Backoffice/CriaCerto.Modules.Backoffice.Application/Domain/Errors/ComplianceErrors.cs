using CriaCerto.BuildingBlocks.Abstractions.Results;

namespace CriaCerto.Modules.Backoffice.Application.Domain.Errors;

public static class ComplianceErrors
{
    public static readonly Error JustificationRequired = Error.Validation(
        "Compliance.JustificationRequired",
        "A justificativa operacional é obrigatória e deve conter no mínimo 10 caracteres para fins de auditoria LGPD.");

    public static readonly Error TargetEntityNotFound = Error.NotFound(
        "Compliance.TargetEntityNotFound",
        "A entidade alvo solicitada para revelação não foi localizada no sistema.");

    public static readonly Error UnsupportedPiiField = Error.Validation(
        "Compliance.UnsupportedPiiField",
        "O campo solicitado não é classificado como dado pessoal sensível (PII) elegível para revelação.");

    public static readonly Error UnauthorizedReveal = Error.Unauthorized(
        "Compliance.UnauthorizedReveal",
        "Acesso negado. A revelação de dados pessoais sensíveis exige a permissão 'compliance.unmask'.");

    public static readonly Error ExportPurposeRequired = Error.Validation(
        "Compliance.ExportPurposeRequired",
        "A finalidade legal da exportação do dossiê (ex: solicitação de titular, fiscalização ANPD ou auditoria externa) é obrigatória.");
}
