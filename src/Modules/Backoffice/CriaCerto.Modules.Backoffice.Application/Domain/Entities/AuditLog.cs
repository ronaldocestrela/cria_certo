using CriaCerto.Modules.Backoffice.Application.Domain.Enums;
using CriaCerto.Modules.Backoffice.Application.Domain.Services;

namespace CriaCerto.Modules.Backoffice.Application.Domain.Entities;

public class AuditLog
{
    public Guid Id { get; private set; }
    public Guid AdminUserId { get; private set; }
    public string AdminUserEmail { get; private set; } = default!;
    public string? ActorRole { get; private set; }
    public string Action { get; private set; } = default!;
    public AuditCategory Category { get; private set; } = AuditCategory.System;
    public AuditSeverity Severity { get; private set; } = AuditSeverity.Medium;
    public string Resource { get; private set; } = default!;
    public Guid? TargetTenantId { get; private set; }
    public string? TargetTenantName { get; private set; }
    public string IpAddress { get; private set; } = default!;
    public string? UserAgent { get; private set; }
    public string? OldValuesJson { get; private set; }
    public string? NewValuesJson { get; private set; }
    public string? DetailsJson { get; private set; }
    public string RecordHash { get; private set; } = default!;
    public string? PreviousRecordHash { get; private set; }
    public bool IsArchived { get; private set; }
    public DateTime TimestampUtc { get; private set; } = DateTime.UtcNow;

    private AuditLog() { }

    public static AuditLog Create(
        Guid adminUserId,
        string adminUserEmail,
        string action,
        string resource,
        string ipAddress,
        string? detailsJson = null)
    {
        var category = InferCategory(action);
        var severity = InferSeverity(action);

        var log = new AuditLog
        {
            Id = Guid.NewGuid(),
            AdminUserId = adminUserId,
            AdminUserEmail = adminUserEmail,
            ActorRole = "Administrator",
            Action = action,
            Category = category,
            Severity = severity,
            Resource = resource,
            TargetTenantId = null,
            TargetTenantName = null,
            IpAddress = ipAddress,
            UserAgent = null,
            OldValuesJson = null,
            NewValuesJson = null,
            DetailsJson = detailsJson,
            PreviousRecordHash = null,
            IsArchived = false,
            TimestampUtc = NormalizeUtc(DateTime.UtcNow)
        };

        log.RecordHash = log.ComputeHash();
        return log;
    }

    public static AuditLog CreateForensic(
        Guid adminUserId,
        string adminUserEmail,
        string? actorRole,
        string action,
        AuditCategory category,
        AuditSeverity severity,
        string resource,
        Guid? targetTenantId,
        string? targetTenantName,
        string ipAddress,
        string? userAgent = null,
        string? oldValuesJson = null,
        string? newValuesJson = null,
        string? previousRecordHash = null,
        string? detailsJson = null)
    {
        var log = new AuditLog
        {
            Id = Guid.NewGuid(),
            AdminUserId = adminUserId,
            AdminUserEmail = adminUserEmail,
            ActorRole = actorRole,
            Action = action,
            Category = category,
            Severity = severity,
            Resource = resource,
            TargetTenantId = targetTenantId,
            TargetTenantName = targetTenantName,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            OldValuesJson = oldValuesJson,
            NewValuesJson = newValuesJson,
            DetailsJson = detailsJson,
            PreviousRecordHash = previousRecordHash,
            IsArchived = false,
            TimestampUtc = NormalizeUtc(DateTime.UtcNow)
        };

        log.RecordHash = log.ComputeHash();
        return log;
    }

    public static DateTime NormalizeUtc(DateTime dt)
    {
        var utc = dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();
        return new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, utc.Second, utc.Millisecond, DateTimeKind.Utc);
    }

    public bool VerifyIntegrity(string? expectedPreviousHash = null)
    {
        if (expectedPreviousHash != null && !string.Equals(PreviousRecordHash, expectedPreviousHash, StringComparison.Ordinal))
        {
            return false;
        }

        var calculated = ComputeHash();
        return string.Equals(RecordHash, calculated, StringComparison.Ordinal);
    }

    public void MarkAsArchived()
    {
        IsArchived = true;
    }

    private string ComputeHash()
    {
        return AuditIntegrityHasher.ComputeHash(
            Id,
            TimestampUtc,
            AdminUserId,
            Action,
            Category,
            Severity,
            Resource,
            TargetTenantId,
            IpAddress,
            UserAgent,
            OldValuesJson,
            NewValuesJson,
            DetailsJson,
            PreviousRecordHash);
    }

    private static AuditCategory InferCategory(string action)
    {
        if (action.StartsWith("Approval.", StringComparison.OrdinalIgnoreCase)) return AuditCategory.Governance;
        if (action.StartsWith("Impersonation.", StringComparison.OrdinalIgnoreCase)) return AuditCategory.Security;
        if (action.StartsWith("AdminUser.", StringComparison.OrdinalIgnoreCase)) return AuditCategory.Security;
        if (action.StartsWith("Tenant.", StringComparison.OrdinalIgnoreCase)) return AuditCategory.TenantManagement;
        if (action.StartsWith("Plan.", StringComparison.OrdinalIgnoreCase) || action.StartsWith("PlanCatalog.", StringComparison.OrdinalIgnoreCase) || action.StartsWith("PlanVersion.", StringComparison.OrdinalIgnoreCase)) return AuditCategory.PlanCatalog;
        if (action.StartsWith("Billing.", StringComparison.OrdinalIgnoreCase)) return AuditCategory.Billing;
        if (action.StartsWith("Support.", StringComparison.OrdinalIgnoreCase)) return AuditCategory.Support;

        return AuditCategory.System;
    }

    private static AuditSeverity InferSeverity(string action)
    {
        if (action.Contains("Suspend", StringComparison.OrdinalIgnoreCase) ||
            action.Contains("ApprovedAndExecuted", StringComparison.OrdinalIgnoreCase) ||
            action.Contains("Impersonation.Started", StringComparison.OrdinalIgnoreCase) ||
            action.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
            action.Contains("Mfa", StringComparison.OrdinalIgnoreCase))
        {
            return AuditSeverity.Critical;
        }

        if (action.Contains("Published", StringComparison.OrdinalIgnoreCase) ||
            action.Contains("Remediation", StringComparison.OrdinalIgnoreCase) ||
            action.Contains("Payment", StringComparison.OrdinalIgnoreCase) ||
            action.Contains("Regularized", StringComparison.OrdinalIgnoreCase))
        {
            return AuditSeverity.High;
        }

        if (action.Contains("Created", StringComparison.OrdinalIgnoreCase) ||
            action.Contains("Updated", StringComparison.OrdinalIgnoreCase))
        {
            return AuditSeverity.Medium;
        }

        return AuditSeverity.Low;
    }
}
