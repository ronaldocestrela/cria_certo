using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Abstractions;
using CriaCerto.Modules.Referral.Application.Domain.Errors;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Referral.Application.Features.RecordReferralClick;

public sealed record RecordReferralClickCommand(string Code) : IRequest<Result>;

public sealed class RecordReferralClickCommandHandler : IRequestHandler<RecordReferralClickCommand, Result>
{
    private readonly IReferralDbContext _dbContext;

    public RecordReferralClickCommandHandler(IReferralDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result> Handle(RecordReferralClickCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return Result.Failure(ReferralErrors.Code.InvalidFormat);
        }

        var normalizedCode = request.Code.Trim().ToUpperInvariant();

        var referralCode = await _dbContext.ReferralCodes
            .FirstOrDefaultAsync(c => c.Code == normalizedCode, cancellationToken);

        if (referralCode == null)
        {
            return Result.Failure(ReferralErrors.Code.NotFound);
        }

        var clickResult = referralCode.RecordClick();
        if (clickResult.IsFailure)
        {
            return clickResult;
        }

        var participant = await _dbContext.Participants
            .FirstOrDefaultAsync(p => p.Id == referralCode.ParticipantId, cancellationToken);
        var channel = participant?.ParticipantType.ToString() ?? "Unknown";

        Referral.Application.Telemetry.ReferralTelemetry.RecordClick(channel, referralCode.Code);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
