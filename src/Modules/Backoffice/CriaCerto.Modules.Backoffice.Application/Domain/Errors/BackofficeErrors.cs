using CriaCerto.BuildingBlocks.Abstractions.Results;

namespace CriaCerto.Modules.Backoffice.Application.Domain.Errors;

public static class BackofficeErrors
{
    public static readonly Error InvalidAdminUserData = Error.Validation(
        "Backoffice.InvalidAdminUserData",
        "Os dados fornecidos para o usuário administrativo são inválidos.");

    public static readonly Error InvalidRoleData = Error.Validation(
        "Backoffice.InvalidRoleData",
        "Os dados do papel administrativo são inválidos.");

    public static readonly Error UserNotFound = Error.NotFound(
        "Backoffice.UserNotFound",
        "O usuário administrativo solicitado não foi encontrado.");

    public static readonly Error RoleNotFound = Error.NotFound(
        "Backoffice.RoleNotFound",
        "O papel administrativo solicitado não foi encontrado.");

    public static readonly Error InvalidPermissionData = Error.Validation(
        "Backoffice.InvalidPermissionData",
        "Os dados fornecidos para a permissão administrativa são inválidos.");

    public static readonly Error InvalidScopeData = Error.Validation(
        "Backoffice.InvalidScopeData",
        "O escopo fornecido para a permissão administrativa é inválido.");

    public static readonly Error UnauthorizedAccess = Error.Unauthorized(
        "Backoffice.UnauthorizedAccess",
        "Acesso negado. Credenciais administrativas ou permissões insuficientes.");
}
