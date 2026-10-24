using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Abstractions;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Features.CommercialPartners.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Referral.Application.Features.CommercialPartners.Queries;

public record PagedResult<T>(
    IReadOnlyCollection<T> Items,
    int TotalCount,
    int Page,
    int PageSize
);

public record GetCommercialPartnersQuery(
    string? SearchTerm = null,
    ParticipantStatus? Status = null,
    CommissionType? CommissionType = null,
    int Page = 1,
    int PageSize = 20
) : IRequest<Result<PagedResult<CommercialPartnerListItemDto>>>;

public sealed class GetCommercialPartnersQueryHandler
    : IRequestHandler<GetCommercialPartnersQuery, Result<PagedResult<CommercialPartnerListItemDto>>>
{
    private readonly IReferralDbContext _dbContext;

    public GetCommercialPartnersQueryHandler(IReferralDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<PagedResult<CommercialPartnerListItemDto>>> Handle(
        GetCommercialPartnersQuery request,
        CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var partnersQuery = from p in _dbContext.Participants
                            join prof in _dbContext.PartnerProfiles on p.Id equals prof.ParticipantId
                            where p.ParticipantType == ParticipantType.CommercialPartner
                            select new { Participant = p, Profile = prof };

        if (request.Status.HasValue)
        {
            partnersQuery = partnersQuery.Where(x => x.Participant.Status == request.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            partnersQuery = partnersQuery.Where(x =>
                x.Profile.FullName.ToLower().Contains(term) ||
                (x.Profile.TradeName != null && x.Profile.TradeName.ToLower().Contains(term)) ||
                x.Profile.Email.ToLower().Contains(term) ||
                x.Profile.PhoneNumber.Contains(term));
        }

        var totalCount = await partnersQuery.CountAsync(cancellationToken);

        var pagedItems = await partnersQuery
            .OrderByDescending(x => x.Profile.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var participantIds = pagedItems.Select(x => x.Participant.Id).ToList();

        var activeContracts = await _dbContext.PartnerContracts
            .Where(c => participantIds.Contains(c.ParticipantId) && c.IsActive)
            .ToListAsync(cancellationToken);

        var referralCodes = await _dbContext.ReferralCodes
            .Where(c => participantIds.Contains(c.ParticipantId) && c.IsActive)
            .ToListAsync(cancellationToken);

        var attributionsCount = await _dbContext.Attributions
            .Where(a => participantIds.Contains(a.ReferrerParticipantId) &&
                        (a.Status == AttributionStatus.Converted || a.Status == AttributionStatus.Pending))
            .GroupBy(a => a.ReferrerParticipantId)
            .Select(g => new { ParticipantId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.ParticipantId, g => g.Count, cancellationToken);

        var resultItems = pagedItems.Select(item =>
        {
            var contract = activeContracts.FirstOrDefault(c => c.ParticipantId == item.Participant.Id);
            var code = referralCodes.FirstOrDefault(c => c.ParticipantId == item.Participant.Id);
            var activeReferrals = attributionsCount.TryGetValue(item.Participant.Id, out var count) ? count : 0;

            return new CommercialPartnerListItemDto(
                item.Profile.Id,
                item.Participant.Id,
                item.Profile.FullName,
                item.Profile.TradeName,
                item.Profile.Email,
                item.Profile.PhoneNumber,
                item.Profile.Category,
                contract?.LegalDocumentNumber ?? string.Empty,
                item.Participant.Status,
                contract?.CommissionType ?? CommissionType.PercentageRecurring,
                contract?.CommissionValue ?? 0,
                contract?.MaxRecurringMonths,
                contract?.PixKey ?? string.Empty,
                contract?.PixKeyType ?? PixKeyType.RandomKey,
                code?.Code ?? string.Empty,
                code?.ClicksCount ?? 0,
                activeReferrals,
                item.Profile.CreatedAtUtc);
        }).ToList();

        if (request.CommissionType.HasValue)
        {
            resultItems = resultItems.Where(i => i.CommissionType == request.CommissionType.Value).ToList();
        }

        return Result.Success(new PagedResult<CommercialPartnerListItemDto>(resultItems, totalCount, page, pageSize));
    }
}
