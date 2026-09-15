using System.Security.Cryptography;
using System.Text;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Enums;

namespace CriaCerto.Modules.Backoffice.Application.Domain.Services;

public class FeatureFlagEvaluator : IFeatureFlagEvaluator
{
    private static readonly HashSet<string> Ring0Roles = new(StringComparer.OrdinalIgnoreCase)
    {
        "PlatformOwner",
        "PlatformAdmin",
        "Admin"
    };

    private static readonly HashSet<string> Ring1Roles = new(StringComparer.OrdinalIgnoreCase)
    {
        "PlatformOwner",
        "PlatformAdmin",
        "Admin",
        "SupportN2",
        "FinanceOps"
    };

    public bool Evaluate(FeatureFlag flag, string adminEmail, string actorRole, Guid? tenantId = null)
    {
        // 1. Kill-Switch tem precedência absoluta
        if (flag.KillSwitchActive)
            return false;

        // 2. Se a flag estiver globalmente desabilitada
        if (!flag.IsEnabled)
            return false;

        var normalizedEmail = adminEmail.Trim().ToLowerInvariant();

        // 3. Whitelist explícita de e-mails para validação e testes
        var whitelisted = flag.GetWhitelistedEmails();
        if (whitelisted.Contains(normalizedEmail, StringComparer.OrdinalIgnoreCase))
            return true;

        // 4. Verificação de Anel de Rollout (Wave / Ring)
        if (!IsRoleEligibleForRing(actorRole, flag.MaxAllowedRing))
            return false;

        // 5. Verificação de percentual determinístico (se < 100%)
        if (flag.RolloutPercentage >= 100)
            return true;

        if (flag.RolloutPercentage <= 0)
            return false;

        var bucket = ComputeDeterministicBucket(flag.Key, normalizedEmail);
        return bucket < flag.RolloutPercentage;
    }

    private static bool IsRoleEligibleForRing(string role, RolloutRing maxAllowedRing)
    {
        return maxAllowedRing switch
        {
            RolloutRing.Ring0_Canary => Ring0Roles.Contains(role),
            RolloutRing.Ring1_EarlyAdopters => Ring1Roles.Contains(role),
            RolloutRing.Ring2_GeneralAvailability => true,
            _ => false
        };
    }

    private static int ComputeDeterministicBucket(string flagKey, string identifier)
    {
        var input = $"{flagKey.Trim().ToLowerInvariant()}:{identifier.Trim().ToLowerInvariant()}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        var uintVal = BitConverter.ToUInt32(hashBytes, 0);
        return (int)(uintVal % 100);
    }
}
