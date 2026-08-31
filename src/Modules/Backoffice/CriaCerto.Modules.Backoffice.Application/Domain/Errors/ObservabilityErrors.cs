using CriaCerto.BuildingBlocks.Abstractions.Results;

namespace CriaCerto.Modules.Backoffice.Application.Domain.Errors;

public static class ObservabilityErrors
{
    public static readonly Error AlertNotFound =
        Error.NotFound("Observability.AlertNotFound", "O alerta operacional informado não foi encontrado.");

    public static readonly Error AlreadyResolved =
        Error.Conflict("Observability.AlreadyResolved", "O alerta operacional já foi resolvido e não permite nova alteração de estado.");

    public static readonly Error CannotAcknowledgeResolved =
        Error.Conflict("Observability.CannotAcknowledgeResolved", "Um alerta resolvido não pode ser marcado como reconhecido.");

    public static readonly Error ResolutionNotesRequired =
        Error.Validation("Observability.ResolutionNotesRequired", "As notas de resolução técnica são obrigatórias e devem possuir no mínimo 5 caracteres.");

    public static readonly Error RuleCodeRequired =
        Error.Validation("Observability.RuleCodeRequired", "O código da regra de alerta é obrigatório.");

    public static readonly Error TitleRequired =
        Error.Validation("Observability.TitleRequired", "O título do alerta operacional é obrigatório.");

    public static readonly Error AdminRequired =
        Error.Validation("Observability.AdminRequired", "O identificador e e-mail do operador responsável são obrigatórios.");
}
