namespace CriaCerto.Modules.Referral.Application.Domain.Services;

public interface IReferralDiscountPolicyResolver
{
    PlanReferralDiscountPolicy? Resolve(
        IEnumerable<PlanReferralDiscountPolicy>? policies,
        string? headTierCode,
        string? legacyPlanCode);
}

