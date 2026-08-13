using System.Security.Claims;
using CriaCerto.Modules.Backoffice.Application.Security;
using Microsoft.AspNetCore.Components.Authorization;

namespace CriaCerto.Web.Client.Services;

public class BackofficePermissionService : IBackofficePermissionService
{
    private readonly AuthenticationStateProvider? _authStateProvider;
    private ClaimsPrincipal? _currentUser;

    public BackofficePermissionService(AuthenticationStateProvider? authStateProvider = null)
    {
        _authStateProvider = authStateProvider;
    }

    public void SetCurrentUser(ClaimsPrincipal user)
    {
        _currentUser = user;
    }

    public async Task<bool> HasPermissionAsync(string permission, string scope = "Global")
    {
        var user = await GetUserAsync();
        if (user is null || user.Identity is null || !user.Identity.IsAuthenticated)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(permission))
        {
            return false;
        }

        // PlatformOwner / SuperAdmin bypass
        if (user.IsInRole(BackofficeRoles.PlatformOwner) ||
            user.HasClaim(c => c.Type == "is_platform_owner" && c.Value.Equals("true", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // Check Roles
        var roles = user.Claims
            .Where(c => c.Type == ClaimTypes.Role || c.Type == "role")
            .Select(c => c.Value)
            .Distinct();

        foreach (var role in roles)
        {
            var rolePermissions = BackofficeRoles.GetDefaultPermissionsForRole(role);
            if (rolePermissions.Contains(permission, StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // Check explicit permission claims
        var permissionClaims = user.Claims
            .Where(c => c.Type.Equals("permission", StringComparison.OrdinalIgnoreCase) ||
                       c.Type.Equals("backoffice_permission", StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Value);

        foreach (var pClaim in permissionClaims)
        {
            if (pClaim.Equals(permission, StringComparison.OrdinalIgnoreCase) || pClaim.Equals("*.*", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (pClaim.Contains(':'))
            {
                var parts = pClaim.Split(':');
                if (parts[0].Equals(permission, StringComparison.OrdinalIgnoreCase) &&
                    (parts[1].Equals(scope, StringComparison.OrdinalIgnoreCase) || parts[1] == "*"))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public async Task<string> GetCurrentUserRoleAsync()
    {
        var user = await GetUserAsync();
        if (user is null || user.Identity is null || !user.Identity.IsAuthenticated)
        {
            return "Convidado";
        }

        var roleClaim = user.FindFirst(ClaimTypes.Role)?.Value ?? user.FindFirst("role")?.Value;
        return roleClaim ?? BackofficeRoles.PlatformOwner;
    }

    private async Task<ClaimsPrincipal?> GetUserAsync()
    {
        if (_currentUser is not null)
        {
            return _currentUser;
        }

        if (_authStateProvider is not null)
        {
            var authState = await _authStateProvider.GetAuthenticationStateAsync();
            if (authState.User.Identity?.IsAuthenticated == true)
            {
                return authState.User;
            }
        }

        return null;
    }
}

