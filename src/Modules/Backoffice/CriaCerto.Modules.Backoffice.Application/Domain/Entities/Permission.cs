using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;

namespace CriaCerto.Modules.Backoffice.Application.Domain.Entities;

public class Permission
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public string Scope { get; private set; } = "Global";

    private Permission() { }

    public static Result<Permission> Create(string name, string description, string scope = "Global")
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(description))
        {
            return Result.Failure<Permission>(BackofficeErrors.InvalidRoleData);
        }

        return Result.Success(new Permission
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Description = description.Trim(),
            Scope = scope.Trim()
        });
    }
}
