namespace CriaCerto.Modules.Backoffice.Application.Security;

public static class BackofficeRoles
{
    public const string PlatformOwner = "PlatformOwner";
    public const string SupportN1 = "SupportN1";
    public const string SupportN2 = "SupportN2";
    public const string FinanceOps = "FinanceOps";
    public const string ReadOnlyAuditor = "ReadOnlyAuditor";

    public static IReadOnlyCollection<string> AllRoles => new[]
    {
        PlatformOwner, SupportN1, SupportN2, FinanceOps, ReadOnlyAuditor
    };

    public static IReadOnlyCollection<string> GetDefaultPermissionsForRole(string roleName)
    {
        return roleName switch
        {
            PlatformOwner => BackofficePermissions.AllPermissions,

            SupportN1 => new[]
            {
                BackofficePermissions.TenantsRead,
                BackofficePermissions.SubscriptionsRead,
                BackofficePermissions.AuditRead
            },

            SupportN2 => new[]
            {
                BackofficePermissions.TenantsRead,
                BackofficePermissions.TenantsWrite,
                BackofficePermissions.SubscriptionsRead,
                BackofficePermissions.SubscriptionsManage,
                BackofficePermissions.AuditRead,
                BackofficePermissions.ImpersonationStart,
                BackofficePermissions.ImpersonationStop
            },

            FinanceOps => new[]
            {
                BackofficePermissions.TenantsRead,
                BackofficePermissions.PlansRead,
                BackofficePermissions.PlansWrite,
                BackofficePermissions.PlansPublish,
                BackofficePermissions.SubscriptionsRead,
                BackofficePermissions.SubscriptionsManage
            },

            ReadOnlyAuditor => new[]
            {
                BackofficePermissions.TenantsRead,
                BackofficePermissions.PlansRead,
                BackofficePermissions.SubscriptionsRead,
                BackofficePermissions.AuditRead
            },

            _ => Array.Empty<string>()
        };
    }
}
