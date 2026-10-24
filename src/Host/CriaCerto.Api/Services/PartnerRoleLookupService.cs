using CriaCerto.Modules.Referral.Application.Abstractions;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Api.Services;

public sealed class PartnerRoleLookupService : IPartnerRoleLookupService
{
    private readonly IReferralDbContext _referralDbContext;

    public PartnerRoleLookupService(IReferralDbContext referralDbContext)
    {
        _referralDbContext = referralDbContext;
    }

    public async Task<bool> IsActiveCommercialPartnerAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await IsActiveCommercialPartnerAsync(userId, null, cancellationToken);
    }

    public async Task<bool> IsActiveCommercialPartnerAsync(Guid userId, string? email, CancellationToken cancellationToken = default)
    {
        if (userId != Guid.Empty)
        {
            var isLinkedAndActive = await _referralDbContext.Participants
                .AnyAsync(p => p.UserId == userId 
                            && p.ParticipantType == ParticipantType.CommercialPartner 
                            && p.Status == ParticipantStatus.Active, 
                    cancellationToken);

            if (isLinkedAndActive)
            {
                return true;
            }
        }

        // Auto-reconciliation: If a Commercial Partner exists with matching normalized email but unlinked UserId, reconcile
        if (!string.IsNullOrWhiteSpace(email))
        {
            var normalizedEmail = email.Trim().ToLowerInvariant();
            var profile = await _referralDbContext.PartnerProfiles
                .FirstOrDefaultAsync(p => p.Email == normalizedEmail, cancellationToken);

            if (profile != null)
            {
                var participant = await _referralDbContext.Participants
                    .FirstOrDefaultAsync(p => p.Id == profile.ParticipantId 
                                           && p.ParticipantType == ParticipantType.CommercialPartner 
                                           && p.Status == ParticipantStatus.Active, 
                        cancellationToken);

                if (participant != null)
                {
                    if (userId != Guid.Empty && participant.UserId != userId)
                    {
                        participant.LinkUserAccount(userId);
                        await _referralDbContext.SaveChangesAsync(cancellationToken);
                    }

                    return true;
                }
            }
        }

        return false;
    }
}
