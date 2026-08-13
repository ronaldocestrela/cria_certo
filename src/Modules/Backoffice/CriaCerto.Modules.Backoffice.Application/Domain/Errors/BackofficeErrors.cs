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

    public static readonly Error MfaRequired = Error.Unauthorized(
        "Backoffice.MfaRequired",
        "Autenticação de dois fatores (MFA) é obrigatória para esta conta administrativa.");

    public static readonly Error InvalidMfaCode = Error.Validation(
        "Backoffice.InvalidMfaCode",
        "O código de verificação MFA fornecido é inválido ou expirou.");

    public static readonly Error MfaAlreadyEnabled = Error.Conflict(
        "Backoffice.MfaAlreadyEnabled",
        "A autenticação de dois fatores (MFA) já está ativada para este usuário.");

    public static readonly Error MfaNotEnabled = Error.Validation(
        "Backoffice.MfaNotEnabled",
        "A autenticação de dois fatores (MFA) não está ativada para este usuário.");

    public static readonly Error InvalidRefreshToken = Error.Unauthorized(
        "Backoffice.InvalidRefreshToken",
        "O refresh token fornecido é inválido ou expirou.");

    public static readonly Error SessionExpired = Error.Unauthorized(
        "Backoffice.SessionExpired",
        "A sessão administrativa expirou.");

    public static readonly Error SessionRevoked = Error.Unauthorized(
        "Backoffice.SessionRevoked",
        "A sessão administrativa foi revogada.");

    public static readonly Error UserDisabled = Error.Unauthorized(
        "Backoffice.UserDisabled",
        "A conta deste usuário administrativo está desativada.");

    public static readonly Error WeakPassword = Error.Validation(
        "Backoffice.WeakPassword",
        "A senha fornecida não atende aos requisitos mínimos de segurança.");
}
