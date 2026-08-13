using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;

namespace CriaCerto.Modules.Backoffice.Application.Domain.Entities;

public class AdminUser
{
    private readonly List<AdminRole> _roles = new();

    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    public string Email { get; private set; } = default!;
    public string PasswordHash { get; private set; } = default!;
    public bool IsActive { get; private set; } = true;
    public bool MfaEnabled { get; private set; } = false;
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? LastLoginAtUtc { get; private set; }
    public IReadOnlyCollection<AdminRole> Roles => _roles.AsReadOnly();

    private AdminUser() { }

    public static Result<AdminUser> Create(string name, string email, string passwordHash)
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
            CreatedAtUtc = DateTime.UtcNow
        });
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

    public void RecordLogin()
    {
        LastLoginAtUtc = DateTime.UtcNow;
    }
}
