using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Abstractions;
using CriaCerto.Modules.Referral.Application.Domain;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Domain.Errors;
using CriaCerto.Modules.Referral.Application.Domain.Services;
using CriaCerto.Modules.Referral.Application.Features.PartnerPortal.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Referral.Application.Features.PartnerPortal.Commands;

public record CreatePartnerCampaignCodeCommand(
    Guid UserId,
    string CampaignTitle,
    string? PreferredSlug
) : IRequest<Result<PartnerCampaignSummaryDto>>;

public sealed class CreatePartnerCampaignCodeCommandHandler
    : IRequestHandler<CreatePartnerCampaignCodeCommand, Result<PartnerCampaignSummaryDto>>
{
    private readonly IReferralDbContext _dbContext;

    public CreatePartnerCampaignCodeCommandHandler(IReferralDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<PartnerCampaignSummaryDto>> Handle(
        CreatePartnerCampaignCodeCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CampaignTitle))
        {
            return Result.Failure<PartnerCampaignSummaryDto>(
                Error.Validation("Campaign.TitleRequired", "O título da campanha é obrigatório."));
        }

        var participant = await _dbContext.Participants
            .FirstOrDefaultAsync(p => p.UserId == request.UserId && p.ParticipantType == ParticipantType.CommercialPartner, cancellationToken);

        if (participant == null)
        {
            return Result.Failure<PartnerCampaignSummaryDto>(ReferralErrors.Participant.NotFound);
        }

        var baseSlug = !string.IsNullOrWhiteSpace(request.PreferredSlug)
            ? ReferralCodeNormalizer.NormalizeSlug(request.PreferredSlug)
            : ReferralCodeNormalizer.NormalizeSlug(request.CampaignTitle);

        if (string.IsNullOrWhiteSpace(baseSlug) || baseSlug.Length < 3)
        {
            return Result.Failure<PartnerCampaignSummaryDto>(ReferralErrors.Code.InvalidFormat);
        }

        var candidateCode = baseSlug;
        var codeExists = await _dbContext.ReferralCodes
            .AnyAsync(c => c.Code == candidateCode, cancellationToken);

        if (codeExists)
        {
            if (!string.IsNullOrWhiteSpace(request.PreferredSlug))
            {
                return Result.Failure<PartnerCampaignSummaryDto>(ReferralErrors.Code.AlreadyExists);
            }

            candidateCode = $"{baseSlug}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}";
        }

        var referralCodeResult = ReferralCode.Create(participant.Id, candidateCode, request.CampaignTitle.Trim());
        if (referralCodeResult.IsFailure)
        {
            return Result.Failure<PartnerCampaignSummaryDto>(referralCodeResult.Error);
        }

        var referralCode = referralCodeResult.Value;
        _dbContext.ReferralCodes.Add(referralCode);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var dto = new PartnerCampaignSummaryDto(
            referralCode.Id,
            referralCode.Code,
            referralCode.CampaignTitle,
            referralCode.ClicksCount,
            referralCode.IsActive,
            referralCode.CreatedAtUtc
        );

        return Result.Success(dto);
    }
}
