namespace CriaCerto.Modules.Backoffice.Application.Security;

public static class BackofficePermissions
{
    // Tenants
    public const string TenantsRead = "tenants.read";
    public const string TenantsWrite = "tenants.write";
    public const string TenantsSuspend = "tenants.suspend";

    // Plans
    public const string PlansRead = "plans.read";
    public const string PlansWrite = "plans.write";
    public const string PlansPublish = "plans.publish";

    // Subscriptions
    public const string SubscriptionsRead = "subscriptions.read";
    public const string SubscriptionsManage = "subscriptions.manage";

    // Impersonation
    public const string ImpersonationStart = "impersonation.start";
    public const string ImpersonationStop = "impersonation.stop";

    // Audit & Admin Users
    public const string AuditRead = "audit.read";
    public const string UsersAdminManage = "users_admin.manage";

    // Scopes
    public const string ScopeGlobal = "Global";
    public const string ScopeTenant = "Tenant";
    public const string ScopeUnidade = "Unidade";

    public static IReadOnlyCollection<string> AllPermissions => new[]
    {
        TenantsRead, TenantsWrite, TenantsSuspend,
        PlansRead, PlansWrite, PlansPublish,
        SubscriptionsRead, SubscriptionsManage,
        ImpersonationStart, ImpersonationStop,
        AuditRead, UsersAdminManage
    };

    public static bool IsValidScope(string scope) =>
        scope is ScopeGlobal or ScopeTenant or ScopeUnidade;
}
