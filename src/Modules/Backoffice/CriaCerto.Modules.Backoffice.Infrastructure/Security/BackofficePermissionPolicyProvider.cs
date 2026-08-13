using CriaCerto.Modules.Backoffice.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace CriaCerto.Modules.Backoffice.Infrastructure.Security;

public class BackofficePermissionPolicyProvider : DefaultAuthorizationPolicyProvider
{
    public BackofficePermissionPolicyProvider(IOptions<AuthorizationOptions> options)
        : base(options)
    {
    }

    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(HasPermissionAttribute.PolicyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var payload = policyName[HasPermissionAttribute.PolicyPrefix.Length..];
            var parts = payload.Split(':');
            var permission = parts[0];
            var scope = parts.Length > 1 ? parts[1] : BackofficePermissions.ScopeGlobal;

            var policy = new AuthorizationPolicyBuilder();
            policy.AddRequirements(new PermissionRequirement(permission, scope));
            return policy.Build();
        }

        return await base.GetPolicyAsync(policyName);
    }
}
