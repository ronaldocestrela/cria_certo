using CriaCerto.BuildingBlocks.Abstractions.Results;

namespace CriaCerto.Modules.Tenancy.Application.Domain.Errors;

public static class TenancyErrors
{
    public static readonly Error TenantNotFound = Error.NotFound(
        "Tenant.NotFound",
        "A organização/fazenda solicitada não foi encontrada.");

    public static readonly Error CnpjAlreadyExists = Error.Conflict(
        "Tenant.CnpjAlreadyExists",
        "Já existe um tenant cadastrado com este CNPJ/CPF.");

    public static readonly Error ExternalIdentifierAlreadyExists = Error.Conflict(
        "Tenant.ExternalIdentifierAlreadyExists",
        "Já existe um tenant cadastrado com este identificador externo.");

    public static readonly Error InvalidCnpj = Error.Validation(
        "Tenant.InvalidCnpj",
        "O CNPJ ou CPF informado é inválido.");

    public static readonly Error CapacityExceedsPlan = Error.Validation(
        "Tenant.CapacityExceedsPlan",
        "A capacidade solicitada excede o limite permitido para o plano contratado.");

    public static readonly Error InvalidTransition = Error.Conflict(
        "Tenant.InvalidTransition",
        "A transição de status solicitada não é permitida para o estado atual do tenant.");

    public static readonly Error JustificationRequired = Error.Validation(
        "Tenant.JustificationRequired",
        "A justificativa é obrigatória e deve conter no mínimo 15 caracteres.");

    public static readonly Error ProtectedTenant = Error.Conflict(
        "Tenant.ProtectedTenant",
        "Este tenant está protegido e não pode receber a ação solicitada.");

    public static readonly Error AlreadyInStatus = Error.Conflict(
        "Tenant.AlreadyInStatus",
        "O tenant já se encontra no status informado.");

    public static readonly Error AlreadyProtected = Error.Conflict(
        "Tenant.AlreadyProtected",
        "O tenant já está marcado como protegido.");

    public static readonly Error AlreadyUnprotected = Error.Conflict(
        "Tenant.AlreadyUnprotected",
        "O tenant já está desprotegido.");

    public static readonly Error TenantNotAccessible = Error.Unauthorized(
        "Tenant.NotAccessible",
        "O acesso a esta organização/fazenda está temporariamente indisponível.");

    public static readonly Error InvalidSegmentation = Error.Validation(
        "Tenant.InvalidSegmentation",
        "Um ou mais valores de segmentação operacional são inválidos.");

    public static readonly Error InvalidTagCategory = Error.Validation(
        "Tenant.InvalidTagCategory",
        "A categoria da etiqueta operacional é inválida.");

    public static readonly Error TagNotFound = Error.NotFound(
        "Tenant.TagNotFound",
        "A etiqueta operacional solicitada não foi encontrada.");

    public static readonly Error TagSlugAlreadyExists = Error.Conflict(
        "Tenant.TagSlugAlreadyExists",
        "Já existe uma etiqueta operacional com este identificador.");

    public static readonly Error TagInactive = Error.Validation(
        "Tenant.TagInactive",
        "A etiqueta operacional está inativa e não pode ser atribuída.");

    public static readonly Error ExportLimitExceeded = Error.Validation(
        "Tenant.ExportLimitExceeded",
        "O recorte de exportação excede o limite máximo de 10.000 registros. Refine os filtros.");

    public static readonly Error PlanVersionNotFound = Error.NotFound(
        "Tenant.PlanVersionNotFound",
        "A versão do plano solicitada não foi encontrada.");

    public static readonly Error PlanVersionNotPublished = Error.Validation(
        "Tenant.PlanVersionNotPublished",
        "Apenas versões de plano publicadas podem ser assinadas.");

    public static readonly Error AlreadySubscribedToPlanVersion = Error.Conflict(
        "Tenant.AlreadySubscribedToPlanVersion",
        "O tenant já está inscrito na versão do plano informada.");

    public static readonly Error GracePeriodActive = Error.Conflict(
        "Tenant.GracePeriodActive",
        "O tenant já se encontra em Grace Period para alteração de plano.");

    public static readonly Error UsageExceedsCapacityRequiresGracePeriod = Error.Validation(
        "Tenant.UsageExceedsCapacityRequiresGracePeriod",
        "O uso atual excede os limites do novo plano. A alteração iniciará um Grace Period de 14 dias.");
}
