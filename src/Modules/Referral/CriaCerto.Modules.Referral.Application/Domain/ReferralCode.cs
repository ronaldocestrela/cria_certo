using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Domain.Errors;

namespace CriaCerto.Modules.Referral.Application.Domain;

public sealed class ReferralCode
{
    public Guid Id { get; private set; }
    public Guid ParticipantId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string? CampaignTitle { get; private set; }
    public long ClicksCount { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private ReferralCode() { }

    public static Result<ReferralCode> Create(Guid participantId, string code, string? campaignTitle = null)
    {
        if (participantId == Guid.Empty)
            return Result.Failure<ReferralCode>(Error.Validation("ReferralCode.ParticipantRequired", "O participante é obrigatório."));

        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length < 3)
            return Result.Failure<ReferralCode>(ReferralErrors.Code.InvalidFormat);

        var normalizedCode = code.Trim().ToUpperInvariant();

        var referralCode = new ReferralCode
        {
            Id = Guid.NewGuid(),
            ParticipantId = participantId,
            Code = normalizedCode,
            CampaignTitle = string.IsNullOrWhiteSpace(campaignTitle) ? null : campaignTitle.Trim(),
            ClicksCount = 0,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        return Result.Success(referralCode);
    }

    public Result RecordClick()
    {
        if (!IsActive)
            return Result.Failure(ReferralErrors.Code.Inactive);

        ClicksCount++;
        return Result.Success();
    }

    public Result Deactivate()
    {
        IsActive = false;
        return Result.Success();
    }

    public Result Reactivate()
    {
        IsActive = true;
        return Result.Success();
    }
}
