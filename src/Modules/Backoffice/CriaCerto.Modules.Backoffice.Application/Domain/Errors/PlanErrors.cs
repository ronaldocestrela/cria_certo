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

    public static readonly Error InvalidHeadTierData = Error.Validation(
        "HeadTierCatalog.InvalidData",
        "Os dados da faixa de cabeças são inválidos.");

    public static readonly Error HeadTierNotFound = Error.NotFound(
        "HeadTierCatalog.NotFound",
        "A faixa de cabeças solicitada não foi encontrada.");

    public static readonly Error HeadTierCodeAlreadyExists = Error.Conflict(
        "HeadTierCatalog.CodeAlreadyExists",
        "Já existe uma faixa de cabeças cadastrada com este código.");

    public static readonly Error InvalidModuleCatalogData = Error.Validation(
        "ModuleCatalog.InvalidData",
        "Os dados do módulo do catálogo são inválidos.");

    public static readonly Error ModuleCatalogNotFound = Error.NotFound(
        "ModuleCatalog.NotFound",
        "O módulo do catálogo solicitado não foi encontrado.");

    public static readonly Error ModuleKeyAlreadyExists = Error.Conflict(
        "ModuleCatalog.KeyAlreadyExists",
        "Já existe um módulo cadastrado com esta chave.");

    public static readonly Error CannotDeactivateCoreModule = Error.Validation(
        "ModuleCatalog.CannotDeactivateCoreModule",
        "O módulo base obrigatório do plantel não pode ser desativado.");

    public static readonly Error InvalidModuleTierPriceData = Error.Validation(
        "ModuleTierPrice.InvalidData",
        "Os dados de precificação do módulo para a faixa são inválidos.");

    public static readonly Error ModuleTierPriceNotFound = Error.NotFound(
        "ModuleTierPrice.NotFound",
        "A versão de preço do módulo para a faixa solicitada não foi encontrada.");
}
