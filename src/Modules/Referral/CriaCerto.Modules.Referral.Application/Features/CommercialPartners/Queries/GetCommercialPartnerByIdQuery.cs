using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Abstractions;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Domain.Errors;
using CriaCerto.Modules.Referral.Application.Features.CommercialPartners.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Referral.Application.Features.CommercialPartners.Queries;

public record GetCommercialPartnerByIdQuery(Guid ParticipantId) : IRequest<Result<CommercialPartnerDto>>;

public sealed class GetCommercialPartnerByIdQueryHandler
    : IRequestHandler<GetCommercialPartnerByIdQuery, Result<CommercialPartnerDto>>
{
    private readonly IReferralDbContext _dbContext;

    public GetCommercialPartnerByIdQueryHandler(IReferralDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<CommercialPartnerDto>> Handle(
        GetCommercialPartnerByIdQuery request,
        CancellationToken cancellationToken)
    {
        var participant = await _dbContext.Participants
            .FirstOrDefaultAsync(p => p.Id == request.ParticipantId, cancellationToken);

        if (participant is null || participant.ParticipantType != ParticipantType.CommercialPartner)
        {
            return Result.Failure<CommercialPartnerDto>(ReferralErrors.PartnerProfile.NotFound);
        }

        var profile = await _dbContext.PartnerProfiles
            .FirstOrDefaultAsync(p => p.ParticipantId == request.ParticipantId, cancellationToken);

        if (profile is null)
        {
            return Result.Failure<CommercialPartnerDto>(ReferralErrors.PartnerProfile.NotFound);
        }

        var contracts = await _dbContext.PartnerContracts
            .Where(c => c.ParticipantId == request.ParticipantId)
            .OrderByDescending(c => c.CreatedAtUtc)
            .Select(c => new PartnerContractHistoryDto(
                c.Id,
                c.CommissionType,
                c.CommissionValue,
                c.MaxRecurringMonths,
                c.PaymentTermsDays,
                c.PixKey,
                c.PixKeyType,
                c.LegalDocumentNumber,
                c.ValidFromUtc,
                c.ValidToUtc,
                c.IsActive,
                c.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        var activeContract = contracts.FirstOrDefault(c => c.IsActive) ?? contracts.FirstOrDefault();

        var referralCode = await _dbContext.ReferralCodes
            .Where(c => c.ParticipantId == request.ParticipantId && c.IsActive)
            .FirstOrDefaultAsync(cancellationToken);

        var dto = new CommercialPartnerDto(
            profile.Id,
            participant.Id,
            participant.UserId,
            profile.FullName,
            profile.TradeName,
            profile.Email,
            profile.PhoneNumber,
            profile.Category,
            profile.City,
            profile.State,
            profile.Notes,
            participant.Status,
            participant.SuspensionReason,
            activeContract?.LegalDocumentNumber ?? string.Empty,
            activeContract?.PixKeyType ?? PixKeyType.RandomKey,
            activeContract?.PixKey ?? string.Empty,
            activeContract?.CommissionType ?? CommissionType.PercentageRecurring,
            activeContract?.CommissionValue ?? 0,
            activeContract?.MaxRecurringMonths,
            activeContract?.PaymentTermsDays ?? 30,
            referralCode?.Code ?? string.Empty,
            referralCode?.ClicksCount ?? 0,
            profile.CreatedAtUtc,
            profile.UpdatedAtUtc,
            contracts);

        return Result.Success(dto);
    }
}
