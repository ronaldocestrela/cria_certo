using CriaCerto.BuildingBlocks.Abstractions.Results;

namespace CriaCerto.Modules.Backoffice.Application.Domain.Errors;

public static class SupportErrors
{
    public static readonly Error TenantNotFound = Error.NotFound(
        "Support.TenantNotFound",
        "O tenant solicitado para diagnóstico ou remediação não foi encontrado.");

    public static readonly Error InvalidTicketId = Error.Validation(
        "Support.InvalidTicketId",
        "O identificador do chamado de suporte (Ticket) é obrigatório e deve ter no mínimo 3 caracteres.");

    public static readonly Error InvalidJustification = Error.Validation(
        "Support.InvalidJustification",
        "A justificativa operacional para a ação remediativa é obrigatória e deve ter no mínimo 10 caracteres.");

    public static readonly Error InvalidActionType = Error.Validation(
        "Support.InvalidActionType",
        "O tipo de ação remediativa solicitado não é suportado pelo catálogo seguro de suporte.");

    public static readonly Error ActionExecutionFailed = Error.Failure(
        "Support.ActionExecutionFailed",
        "Falha técnica ao executar a ação remediativa de suporte.");

    public static readonly Error UnauthorizedRemediation = Error.Unauthorized(
        "Support.UnauthorizedRemediation",
        "Acesso negado: apenas operadores N2 e PlatformOwner possuem autorização para executar ações remediativas.");
}
