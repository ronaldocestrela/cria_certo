namespace CriaCerto.Modules.Backoffice.Application.Features.AdminUsers.Dtos;

public record AdminUserSummaryDto(
    Guid Id,
    string Name,
    string Email,
    bool IsActive,
    bool MfaEnabled,
    bool RequiresMfa,
    bool MustChangePasswordOnNextLogin,
    DateTime CreatedAtUtc,
    DateTime? LastLoginAtUtc,
    IReadOnlyCollection<string> Roles
);

public record AdminUserDetailDto(
    Guid Id,
    string Name,
    string Email,
    bool IsActive,
    bool MfaEnabled,
    bool RequiresMfa,
    bool MustChangePasswordOnNextLogin,
    DateTime CreatedAtUtc,
    DateTime? LastLoginAtUtc,
    IReadOnlyCollection<AdminRoleSummaryDto> Roles,
    IReadOnlyCollection<AdminSessionDto> ActiveSessions
);

public record AdminRoleSummaryDto(
    Guid Id,
    string Name,
    string Description,
    IReadOnlyCollection<string> Permissions
);

public record AdminSessionDto(
    Guid Id,
    Guid AdminUserId,
    string SessionToken,
    string IpAddress,
    string UserAgent,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc,
    DateTime RefreshTokenExpiresAtUtc,
    bool IsRevoked,
    bool IsActive
);

public record MfaSetupResultDto(
    string SecretKey,
    string QrCodeUri,
    IReadOnlyCollection<string> RecoveryCodes
);

public record AdminAuthResultDto(
    string SessionToken,
    string RefreshToken,
    DateTime ExpiresAtUtc,
    AdminUserSummaryDto User
);
