using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;

namespace CriaCerto.Modules.Backoffice.Application.Domain.Entities;

public class AdminUser
{
    private static readonly HashSet<string> SensitivePermissions = new(StringComparer.OrdinalIgnoreCase)
    {
        Security.BackofficePermissions.ImpersonationStart,
        Security.BackofficePermissions.ImpersonationStop,
        Security.BackofficePermissions.PlansPublish,
        Security.BackofficePermissions.TenantsSuspend
    };

    private readonly List<AdminRole> _roles = new();
    private readonly List<string> _recoveryCodes = new();

    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    public string Email { get; private set; } = default!;
    public string PasswordHash { get; private set; } = default!;
    public bool IsActive { get; private set; } = true;
    public bool MfaEnabled { get; private set; } = false;
    public string? MfaSecretKey { get; private set; }
    public IReadOnlyCollection<string> RecoveryCodes => _recoveryCodes.AsReadOnly();
    public bool MustChangePasswordOnNextLogin { get; private set; } = false;
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? LastLoginAtUtc { get; private set; }
    public IReadOnlyCollection<AdminRole> Roles => _roles.AsReadOnly();

    private AdminUser() { }

    public static Result<AdminUser> Create(string name, string email, string passwordHash, bool mustChangePasswordOnNextLogin = false)
    {
        if (string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(email) ||
            !email.Contains('@') ||
            string.IsNullOrWhiteSpace(passwordHash))
        {
            return Result.Failure<AdminUser>(BackofficeErrors.InvalidAdminUserData);
        }

        return Result.Success(new AdminUser
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHash,
            IsActive = true,
            MfaEnabled = false,
            MustChangePasswordOnNextLogin = mustChangePasswordOnNextLogin,
            CreatedAtUtc = DateTime.UtcNow
        });
    }

    public Result UpdateDetails(string name, string email)
    {
        if (string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(email) ||
            !email.Contains('@'))
        {
            return Result.Failure(BackofficeErrors.InvalidAdminUserData);
        }

        Name = name.Trim();
        Email = email.Trim().ToLowerInvariant();
        return Result.Success();
    }

    public Result UpdatePasswordHash(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
        {
            return Result.Failure(BackofficeErrors.WeakPassword);
        }

        PasswordHash = newPasswordHash;
        MustChangePasswordOnNextLogin = false;
        return Result.Success();
    }

    public Result EnableMfa(string secretKey, IEnumerable<string> recoveryCodes)
    {
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            return Result.Failure(BackofficeErrors.InvalidMfaCode);
        }

        MfaSecretKey = secretKey;
        MfaEnabled = true;
        _recoveryCodes.Clear();
        if (recoveryCodes != null)
        {
            _recoveryCodes.AddRange(recoveryCodes);
        }

        return Result.Success();
    }

    public Result DisableMfa()
    {
        MfaEnabled = false;
        MfaSecretKey = null;
        _recoveryCodes.Clear();
        return Result.Success();
    }

    public Result Deactivate()
    {
        IsActive = false;
        return Result.Success();
    }

    public Result Activate()
    {
        IsActive = true;
        return Result.Success();
    }

    public Result AssignRole(AdminRole role)
    {
        if (role is null)
        {
            return Result.Failure(BackofficeErrors.InvalidRoleData);
        }

        if (!_roles.Any(r => r.Id == role.Id || r.Name.Equals(role.Name, StringComparison.OrdinalIgnoreCase)))
        {
            _roles.Add(role);
        }

        return Result.Success();
    }

    public Result RemoveRole(Guid roleId)
    {
        _roles.RemoveAll(r => r.Id == roleId);
        return Result.Success();
    }

    public bool RequiresMfa()
    {
        return _roles.Any(r => r.Permissions.Any(p => SensitivePermissions.Contains(p.Name)));
    }

    public void RecordLogin()
    {
        LastLoginAtUtc = DateTime.UtcNow;
    }
}
