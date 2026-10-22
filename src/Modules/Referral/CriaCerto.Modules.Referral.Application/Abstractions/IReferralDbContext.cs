using CriaCerto.Modules.Referral.Application.Domain;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Referral.Application.Abstractions;

public interface IReferralDbContext
{
    DbSet<ReferralProgramParticipant> Participants { get; }
    DbSet<ReferralCode> ReferralCodes { get; }
    DbSet<CommercialPartnerProfile> PartnerProfiles { get; }
    DbSet<PartnerContract> PartnerContracts { get; }
    DbSet<PlanReferralDiscountPolicy> PlanDiscountPolicies { get; }
    DbSet<ReferralAttribution> Attributions { get; }
    DbSet<ReferralRewardLedger> RewardLedger { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
