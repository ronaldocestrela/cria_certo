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
        if (userId == Guid.Empty)
        {
            return false;
        }

        return await _referralDbContext.Participants
            .AnyAsync(p => p.UserId == userId 
                        && p.ParticipantType == ParticipantType.CommercialPartner 
                        && p.Status == ParticipantStatus.Active, 
                cancellationToken);
    }
}
