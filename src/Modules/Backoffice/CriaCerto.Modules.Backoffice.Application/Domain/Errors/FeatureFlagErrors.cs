using CriaCerto.BuildingBlocks.Abstractions.Results;

namespace CriaCerto.Modules.Backoffice.Application.Domain.Errors;

public static class FeatureFlagErrors
{
    public static readonly Error KeyRequired = Error.Validation(
        "FeatureFlag.KeyRequired",
        "A chave identificadora da feature flag é obrigatória e não pode ser vazia.");

    public static readonly Error NameRequired = Error.Validation(
        "FeatureFlag.NameRequired",
        "O nome descritivo da feature flag é obrigatório.");

    public static readonly Error InvalidRolloutPercentage = Error.Validation(
        "FeatureFlag.InvalidRolloutPercentage",
        "O percentual de rollout deve ser um valor inteiro entre 0 e 100.");

    public static readonly Error JustificationTooShort = Error.Validation(
        "FeatureFlag.JustificationTooShort",
        "A justificativa operacional é obrigatória e deve possuir no mínimo 10 caracteres.");

    public static readonly Error NotFound = Error.NotFound(
        "FeatureFlag.NotFound",
        "A feature flag solicitada não foi encontrada no catálogo de governança.");

    public static readonly Error FeatureFlagDisabled = Error.Conflict(
        "FeatureFlag.Disabled",
        "Esta funcionalidade está temporariamente desativada pela governança de rollout.");

    public static readonly Error KillSwitchActive = Error.Conflict(
        "FeatureFlag.KillSwitchActive",
        "Esta funcionalidade foi interrompida em regime de emergência (Kill-Switch ativo).");

    public static readonly Error WaveNotReached = Error.Conflict(
        "FeatureFlag.WaveNotReached",
        "O seu perfil de operador ainda não foi contemplado na onda de liberação gradual desta funcionalidade.");

    public static readonly Error SloBreachDetected = Error.Conflict(
        "FeatureFlag.SloBreachDetected",
        "Operação suspensa temporariamente devido à violação de SLO de erro ou latência na onda atual.");
}
