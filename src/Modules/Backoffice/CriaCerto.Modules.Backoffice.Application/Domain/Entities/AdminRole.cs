using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;

namespace CriaCerto.Modules.Backoffice.Application.Domain.Entities;

public class AdminRole
{
    private readonly List<Permission> _permissions = new();

    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public IReadOnlyCollection<Permission> Permissions => _permissions.AsReadOnly();

    private AdminRole() { }

    public static Result<AdminRole> Create(string name, string description)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(description))
        {
            return Result.Failure<AdminRole>(BackofficeErrors.InvalidRoleData);
        }

        return Result.Success(new AdminRole
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Description = description.Trim()
        });
    }

    public Result AddPermission(Permission permission)
    {
        if (permission is null)
        {
            return Result.Failure(BackofficeErrors.InvalidPermissionData);
        }

        if (!_permissions.Any(p => p.Name.Equals(permission.Name, StringComparison.OrdinalIgnoreCase) &&
                                   p.Scope.Equals(permission.Scope, StringComparison.OrdinalIgnoreCase)))
        {
            _permissions.Add(permission);
        }

        return Result.Success();
    }

    public Result RemovePermission(string permissionName, string scope = "Global")
    {
        if (string.IsNullOrWhiteSpace(permissionName))
        {
            return Result.Failure(BackofficeErrors.InvalidPermissionData);
        }

        var existing = _permissions.FirstOrDefault(p => p.Name.Equals(permissionName, StringComparison.OrdinalIgnoreCase) &&
                                                        p.Scope.Equals(scope, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            _permissions.Remove(existing);
        }

        return Result.Success();
    }
}
