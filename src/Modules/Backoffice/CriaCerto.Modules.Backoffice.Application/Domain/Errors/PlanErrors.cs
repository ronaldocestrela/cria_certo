using CriaCerto.BuildingBlocks.Abstractions.Results;

namespace CriaCerto.Modules.Backoffice.Application.Domain.Errors;

public static class PlanErrors
{
    public static readonly Error InvalidPlanData = Error.Validation(
        "PlanCatalog.InvalidPlanData",
        "Os dados do plano são inválidos.");

    public static readonly Error InvalidVersionData = Error.Validation(
        "PlanCatalog.InvalidVersionData",
        "Os dados da versão do plano são inválidos.");

    public static readonly Error PlanNotFound = Error.NotFound(
        "PlanCatalog.PlanNotFound",
        "O plano solicitado não foi encontrado.");

    public static readonly Error VersionNotFound = Error.NotFound(
        "PlanCatalog.VersionNotFound",
        "A versão do plano solicitada não foi encontrada.");

    public static readonly Error PublishedVersionImmutable = Error.Conflict(
        "PlanCatalog.PublishedVersionImmutable",
        "Versões publicadas são imutáveis e não podem ser alteradas.");

    public static readonly Error DraftAlreadyExists = Error.Conflict(
        "PlanCatalog.DraftAlreadyExists",
        "Já existe uma versão em rascunho (Draft) para este plano.");

    public static readonly Error VersionNotDraft = Error.Validation(
        "PlanCatalog.VersionNotDraft",
        "Apenas versões em rascunho (Draft) podem ser publicadas.");

    public static readonly Error CannotDeprecateNonPublished = Error.Validation(
        "PlanCatalog.CannotDeprecateNonPublished",
        "Apenas versões publicadas podem ser marcadas como obsoletas (Deprecated).");
}
