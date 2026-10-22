using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Abstractions;
using CriaCerto.Modules.Referral.Application.Domain.Errors;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Referral.Application.Features.ValidateReferralCode;

public sealed record ReferralCodeValidationDto(
    string Code,
    bool IsValid,
    string? CampaignTitle,
    string ParticipantType
);

public sealed record ValidateReferralCodeQuery(string Code) : IRequest<Result<ReferralCodeValidationDto>>;

public sealed class ValidateReferralCodeQueryHandler : IRequestHandler<ValidateReferralCodeQuery, Result<ReferralCodeValidationDto>>
{
    private readonly IReferralDbContext _dbContext;

    public ValidateReferralCodeQueryHandler(IReferralDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<ReferralCodeValidationDto>> Handle(ValidateReferralCodeQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return Result.Failure<ReferralCodeValidationDto>(ReferralErrors.Code.InvalidFormat);
        }

        var normalizedCode = request.Code.Trim().ToUpperInvariant();

        var referralCode = await _dbContext.ReferralCodes
            .FirstOrDefaultAsync(c => c.Code == normalizedCode, cancellationToken);

        if (referralCode == null)
        {
            return Result.Failure<ReferralCodeValidationDto>(ReferralErrors.Code.NotFound);
        }

        if (!referralCode.IsActive)
        {
            return Result.Failure<ReferralCodeValidationDto>(ReferralErrors.Code.Inactive);
        }

        var participant = await _dbContext.Participants
            .FirstOrDefaultAsync(p => p.Id == referralCode.ParticipantId, cancellationToken);

        var participantType = participant?.ParticipantType.ToString() ?? "Unknown";

        var dto = new ReferralCodeValidationDto(
            Code: referralCode.Code,
            IsValid: true,
            CampaignTitle: referralCode.CampaignTitle,
            ParticipantType: participantType
        );

        return Result.Success(dto);
    }
}
