using System.Security.Claims;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Security;

namespace CriaCerto.Modules.Backoffice.Infrastructure.Security;

public class PermissionEvaluatorService : IPermissionEvaluator
{
    public Result<bool> HasPermission(ClaimsPrincipal? user, string permissionName, string requiredScope = BackofficePermissions.ScopeGlobal)
    {
        if (user is null || user.Identity is null || !user.Identity.IsAuthenticated)
        {
            return Result.Success(false);
        }

        if (string.IsNullOrWhiteSpace(permissionName))
        {
            return Result.Success(false);
        }

        // 1. PlatformOwner / SuperAdmin bypass
        if (user.IsInRole(BackofficeRoles.PlatformOwner) ||
            user.HasClaim(c => c.Type == "is_platform_owner" && c.Value.Equals("true", StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Success(true);
        }

        // 2. Check Role-based default permissions
        var roles = user.Claims
            .Where(c => c.Type == ClaimTypes.Role || c.Type == "role")
            .Select(c => c.Value)
            .Distinct();

        foreach (var role in roles)
        {
            var rolePermissions = BackofficeRoles.GetDefaultPermissionsForRole(role);
            if (rolePermissions.Contains(permissionName, StringComparer.OrdinalIgnoreCase))
            {
                return Result.Success(true);
            }
        }

        // 3. Check explicit permission claims (permission: "tenants.read" or "permission" = "tenants.read")
        var permissionClaims = user.Claims
            .Where(c => c.Type.Equals("permission", StringComparison.OrdinalIgnoreCase) ||
                       c.Type.Equals("backoffice_permission", StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Value);

        foreach (var pClaim in permissionClaims)
        {
            if (pClaim.Equals(permissionName, StringComparison.OrdinalIgnoreCase) ||
                pClaim.Equals($"*.*", StringComparison.OrdinalIgnoreCase))
            {
                return Result.Success(true);
            }

            // Support formatted claim "permissionName:Scope" e.g., "tenants.read:Global"
            if (pClaim.Contains(':'))
            {
                var parts = pClaim.Split(':');
                if (parts[0].Equals(permissionName, StringComparison.OrdinalIgnoreCase) &&
                    (parts[1].Equals(requiredScope, StringComparison.OrdinalIgnoreCase) || parts[1] == "*"))
                {
                    return Result.Success(true);
                }
            }
        }

        return Result.Success(false);
    }
}
