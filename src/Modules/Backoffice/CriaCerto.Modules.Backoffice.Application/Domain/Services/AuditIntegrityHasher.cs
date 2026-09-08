using System.Security.Cryptography;
using System.Text;
using CriaCerto.Modules.Backoffice.Application.Domain.Enums;

namespace CriaCerto.Modules.Backoffice.Application.Domain.Services;

public static class AuditIntegrityHasher
{
    public static string ComputeHash(
        Guid id,
        DateTime timestampUtc,
        Guid adminUserId,
        string action,
        AuditCategory category,
        AuditSeverity severity,
        string resource,
        Guid? targetTenantId,
        string ipAddress,
        string? userAgent,
        string? oldValuesJson,
        string? newValuesJson,
        string? detailsJson,
        string? previousRecordHash)
    {
        var canonicalString = string.Join("|",
            id.ToString("D"),
            timestampUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            adminUserId.ToString("D"),
            action?.Trim() ?? string.Empty,
            ((int)category).ToString(),
            ((int)severity).ToString(),
            resource?.Trim() ?? string.Empty,
            targetTenantId?.ToString("D") ?? string.Empty,
            ipAddress?.Trim() ?? string.Empty,
            userAgent?.Trim() ?? string.Empty,
            oldValuesJson?.Trim() ?? string.Empty,
            newValuesJson?.Trim() ?? string.Empty,
            detailsJson?.Trim() ?? string.Empty,
            previousRecordHash?.Trim() ?? string.Empty);

        var bytes = Encoding.UTF8.GetBytes(canonicalString);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }
}
