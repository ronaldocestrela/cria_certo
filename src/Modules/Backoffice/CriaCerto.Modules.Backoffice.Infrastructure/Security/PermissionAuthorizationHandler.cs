using CriaCerto.Modules.Backoffice.Application.Security;
using Microsoft.AspNetCore.Authorization;

namespace CriaCerto.Modules.Backoffice.Infrastructure.Security;

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IPermissionEvaluator _permissionEvaluator;

    public PermissionAuthorizationHandler(IPermissionEvaluator permissionEvaluator)
    {
        _permissionEvaluator = permissionEvaluator;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var evaluation = _permissionEvaluator.HasPermission(context.User, requirement.Permission, requirement.Scope);

        if (evaluation.IsSuccess && evaluation.Value)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
