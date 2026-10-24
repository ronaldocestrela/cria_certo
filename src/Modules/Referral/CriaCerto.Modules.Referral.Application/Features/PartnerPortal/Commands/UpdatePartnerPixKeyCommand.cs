using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Abstractions;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Domain.Errors;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Referral.Application.Features.PartnerPortal.Commands;

public record UpdatePartnerPixKeyCommand(
    Guid UserId,
    PixKeyType PixKeyType,
    string PixKey
) : IRequest<Result>;

public sealed class UpdatePartnerPixKeyCommandHandler : IRequestHandler<UpdatePartnerPixKeyCommand, Result>
{
    private readonly IReferralDbContext _dbContext;

    public UpdatePartnerPixKeyCommandHandler(IReferralDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result> Handle(UpdatePartnerPixKeyCommand request, CancellationToken cancellationToken)
    {
        var participant = await _dbContext.Participants
            .FirstOrDefaultAsync(p => p.UserId == request.UserId && p.ParticipantType == ParticipantType.CommercialPartner, cancellationToken);

        if (participant == null)
        {
            return Result.Failure(ReferralErrors.Participant.NotFound);
        }

        var contract = await _dbContext.PartnerContracts
            .FirstOrDefaultAsync(c => c.ParticipantId == participant.Id && c.IsActive, cancellationToken);

        if (contract == null)
        {
            return Result.Failure(ReferralErrors.Contract.NotFound);
        }

        var updateResult = contract.UpdatePixKey(request.PixKeyType, request.PixKey);
        if (updateResult.IsFailure)
        {
            return updateResult;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
