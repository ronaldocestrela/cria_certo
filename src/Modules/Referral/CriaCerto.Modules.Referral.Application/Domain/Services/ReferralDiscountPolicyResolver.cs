using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Tenancy.Application.Domain;

namespace CriaCerto.Modules.Referral.Application.Domain.Services;

public sealed class ReferralDiscountPolicyResolver : IReferralDiscountPolicyResolver
{
    public PlanReferralDiscountPolicy? Resolve(
        IEnumerable<PlanReferralDiscountPolicy>? policies,
        string? headTierCode,
        string? legacyPlanCode)
    {
        if (policies == null)
        {
            return null;
        }

        var activePolicies = policies.Where(p => p.IsActive).ToList();
        if (activePolicies.Count == 0)
        {
            return null;
        }

        // Prioridade 1: Faixa de Cabeças Específica (ex: TIER_500, TIER_1500, etc.)
        if (!string.IsNullOrWhiteSpace(headTierCode))
        {
            var normalizedTier = HeadTierCapacityLimits.IsValidHeadTierCode(headTierCode)
                ? HeadTierCapacityLimits.NormalizeHeadTierCode(headTierCode)
                : headTierCode.Trim().ToUpperInvariant();

            var tierPolicy = activePolicies.FirstOrDefault(p =>
                (p.HeadTierCode != null && p.HeadTierCode.Equals(normalizedTier, StringComparison.OrdinalIgnoreCase)) ||
                (p.PlanCode.Equals(normalizedTier, StringComparison.OrdinalIgnoreCase) && p.DiscountScope == DiscountPolicyScope.HeadTier));

            if (tierPolicy != null)
            {
                return tierPolicy;
            }
        }

        // Prioridade 2: Política Padrão Global (GLOBAL_DEFAULT)
        var globalPolicy = activePolicies.FirstOrDefault(p =>
            p.DiscountScope == DiscountPolicyScope.GlobalDefault ||
            p.PlanCode.Equals(PlanReferralDiscountPolicy.GlobalDefaultCode, StringComparison.OrdinalIgnoreCase));

        if (globalPolicy != null)
        {
            return globalPolicy;
        }

        // Prioridade 3: Plano Legado (ex: STARTER, PRO, ENTERPRISE)
        if (!string.IsNullOrWhiteSpace(legacyPlanCode))
        {
            var normalizedLegacy = legacyPlanCode.Trim().ToUpperInvariant();
            var legacyPolicy = activePolicies.FirstOrDefault(p =>
                p.PlanCode.Equals(normalizedLegacy, StringComparison.OrdinalIgnoreCase));

            if (legacyPolicy != null)
            {
                return legacyPolicy;
            }
        }

        return null;
    }
}

