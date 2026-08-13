using CriaCerto.Modules.Backoffice.Application.Security;
using Microsoft.AspNetCore.Authorization;

namespace CriaCerto.Modules.Backoffice.Infrastructure.Security;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public class HasPermissionAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "Permission:";

    public HasPermissionAttribute(string permission, string scope = BackofficePermissions.ScopeGlobal)
        : base(policy: $"{PolicyPrefix}{permission}:{scope}")
    {
    }
}
