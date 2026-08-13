using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;
using CriaCerto.Modules.Backoffice.Application.Security;

namespace CriaCerto.Modules.Backoffice.Application.Domain.Entities;

public class Permission
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public string Scope { get; private set; } = BackofficePermissions.ScopeGlobal;

    private Permission() { }

    public static Result<Permission> Create(string name, string description, string scope = BackofficePermissions.ScopeGlobal)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(description))
        {
            return Result.Failure<Permission>(BackofficeErrors.InvalidPermissionData);
        }

        var normalizedScope = string.IsNullOrWhiteSpace(scope) ? BackofficePermissions.ScopeGlobal : scope.Trim();

        if (!BackofficePermissions.IsValidScope(normalizedScope))
        {
            return Result.Failure<Permission>(BackofficeErrors.InvalidScopeData);
        }

        return Result.Success(new Permission
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Description = description.Trim(),
            Scope = normalizedScope
        });
    }
}
