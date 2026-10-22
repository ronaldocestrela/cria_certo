using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Abstractions;
using CriaCerto.Modules.Referral.Application.Domain.Errors;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Referral.Application.Features.CommercialPartners.Commands;

public record SuspendCommercialPartnerCommand(
    Guid ParticipantId,
    string Reason,
    Guid AdminUserId
) : IRequest<Result>;

public sealed class SuspendCommercialPartnerCommandValidator
    : AbstractValidator<SuspendCommercialPartnerCommand>
{
    public SuspendCommercialPartnerCommandValidator()
    {
        RuleFor(x => x.ParticipantId)
            .NotEmpty().WithMessage("O identificador do participante é obrigatório.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("A justificativa para a suspensão é obrigatória.")
            .MinimumLength(5).WithMessage("A justificativa deve conter ao menos 5 caracteres.");

        RuleFor(x => x.AdminUserId)
            .NotEmpty().WithMessage("O identificador do administrador é obrigatório.");
    }
}

public sealed class SuspendCommercialPartnerCommandHandler
    : IRequestHandler<SuspendCommercialPartnerCommand, Result>
{
    private readonly IReferralDbContext _dbContext;

    public SuspendCommercialPartnerCommandHandler(IReferralDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result> Handle(
        SuspendCommercialPartnerCommand request,
        CancellationToken cancellationToken)
    {
        var participant = await _dbContext.Participants
            .FirstOrDefaultAsync(p => p.Id == request.ParticipantId, cancellationToken);

        if (participant is null)
        {
            return Result.Failure(ReferralErrors.PartnerProfile.NotFound);
        }

        var suspendResult = participant.Suspend(request.Reason);
        if (suspendResult.IsFailure)
        {
            return suspendResult;
        }

        // Deactivate active referral codes while suspended
        var activeCodes = await _dbContext.ReferralCodes
            .Where(c => c.ParticipantId == request.ParticipantId && c.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var code in activeCodes)
        {
            code.Deactivate();
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
