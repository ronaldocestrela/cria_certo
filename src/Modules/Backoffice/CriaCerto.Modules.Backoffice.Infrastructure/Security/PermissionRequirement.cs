using CriaCerto.Modules.Backoffice.Application.Security;
using Microsoft.AspNetCore.Authorization;

namespace CriaCerto.Modules.Backoffice.Infrastructure.Security;

public class PermissionRequirement : IAuthorizationRequirement
{
    public string Permission { get; }
    public string Scope { get; }

    public PermissionRequirement(string permission, string scope = BackofficePermissions.ScopeGlobal)
    {
        Permission = permission ?? throw new ArgumentNullException(nameof(permission));
        Scope = scope ?? BackofficePermissions.ScopeGlobal;
    }
}
