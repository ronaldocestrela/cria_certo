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

    // Support Workbench
    public const string SupportDiagnose = "support.diagnose";
    public const string SupportRemediate = "support.remediate";

    // 4-Eyes Administrative Approvals
    public const string ApprovalsRequest = "approvals.request";
    public const string ApprovalsReview = "approvals.review";

    // Audit & Admin Users
    public const string AuditRead = "audit.read";
    public const string UsersAdminManage = "users_admin.manage";

    // Observability & Alerts
    public const string ObservabilityRead = "observability.read";
    public const string ObservabilityManage = "observability.manage";

    // Compliance & LGPD Data Governance
    public const string ComplianceRead = "compliance.read";
    public const string ComplianceExport = "compliance.export";
    public const string ComplianceUnmask = "compliance.unmask";

    // Wave Rollout & Feature Flags
    public const string RolloutRead = "rollout.read";
    public const string RolloutManage = "rollout.manage";
    public const string RolloutKillSwitch = "rollout.killswitch";

    // Referral Partners Management
    public const string ReferralPartnersRead = "referral.partners.read";
    public const string ReferralPartnersManage = "referral.partners.manage";

    // Referral Payouts & Commissions Management
    public const string ReferralPayoutsRead = "referral.payouts.read";
    public const string ReferralPayoutsManage = "referral.payouts.manage";
    public const string ReferralPayoutsExport = "referral.payouts.export";

    // Referral Plan Discounts Management
    public const string ReferralDiscountsRead = "referral.discounts.read";
    public const string ReferralDiscountsManage = "referral.discounts.manage";

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
        SupportDiagnose, SupportRemediate,
        ApprovalsRequest, ApprovalsReview,
        AuditRead, UsersAdminManage,
        ObservabilityRead, ObservabilityManage,
        ComplianceRead, ComplianceExport, ComplianceUnmask,
        RolloutRead, RolloutManage, RolloutKillSwitch,
        ReferralPartnersRead, ReferralPartnersManage,
        ReferralPayoutsRead, ReferralPayoutsManage, ReferralPayoutsExport,
        ReferralDiscountsRead, ReferralDiscountsManage
    };

    public static bool IsValidScope(string scope) =>
        scope is ScopeGlobal or ScopeTenant or ScopeUnidade;
}
