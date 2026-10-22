using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Abstractions;
using CriaCerto.Modules.Referral.Application.Domain.Errors;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Referral.Application.Features.CommercialPartners.Commands;

public record ReactivateCommercialPartnerCommand(
    Guid ParticipantId,
    Guid AdminUserId
) : IRequest<Result>;

public sealed class ReactivateCommercialPartnerCommandValidator
    : AbstractValidator<ReactivateCommercialPartnerCommand>
{
    public ReactivateCommercialPartnerCommandValidator()
    {
        RuleFor(x => x.ParticipantId)
            .NotEmpty().WithMessage("O identificador do participante é obrigatório.");

        RuleFor(x => x.AdminUserId)
            .NotEmpty().WithMessage("O identificador do administrador é obrigatório.");
    }
}

public sealed class ReactivateCommercialPartnerCommandHandler
    : IRequestHandler<ReactivateCommercialPartnerCommand, Result>
{
    private readonly IReferralDbContext _dbContext;

    public ReactivateCommercialPartnerCommandHandler(IReferralDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result> Handle(
        ReactivateCommercialPartnerCommand request,
        CancellationToken cancellationToken)
    {
        var participant = await _dbContext.Participants
            .FirstOrDefaultAsync(p => p.Id == request.ParticipantId, cancellationToken);

        if (participant is null)
        {
            return Result.Failure(ReferralErrors.PartnerProfile.NotFound);
        }

        var reactivateResult = participant.Reactivate();
        if (reactivateResult.IsFailure)
        {
            return reactivateResult;
        }

        // Reactivate referral codes
        var codes = await _dbContext.ReferralCodes
            .Where(c => c.ParticipantId == request.ParticipantId)
            .ToListAsync(cancellationToken);

        foreach (var code in codes)
        {
            code.Reactivate();
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
