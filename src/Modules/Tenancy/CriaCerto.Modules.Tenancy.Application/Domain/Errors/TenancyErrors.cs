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
}
