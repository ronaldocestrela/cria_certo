using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Abstractions;
using CriaCerto.Modules.Referral.Application.Domain.Errors;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Referral.Application.Features.Payouts.Commands;

public sealed record CancelCommissionLedgerEntryCommand(
    Guid LedgerId,
    string Reason,
    Guid AdminId
) : IRequest<Result>;

public sealed class CancelCommissionLedgerEntryCommandValidator : AbstractValidator<CancelCommissionLedgerEntryCommand>
{
    public CancelCommissionLedgerEntryCommandValidator()
    {
        RuleFor(x => x.LedgerId)
            .NotEmpty().WithMessage("O identificador do lançamento no livro-razão é obrigatório.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("A justificativa para cancelamento da comissão é obrigatória.")
            .MinimumLength(10).WithMessage("A justificativa deve conter ao menos 10 caracteres.")
            .MaximumLength(500).WithMessage("A justificativa não pode exceder 500 caracteres.");
    }
}

public sealed class CancelCommissionLedgerEntryCommandHandler
    : IRequestHandler<CancelCommissionLedgerEntryCommand, Result>
{
    private readonly IReferralDbContext _dbContext;

    public CancelCommissionLedgerEntryCommandHandler(IReferralDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result> Handle(
        CancelCommissionLedgerEntryCommand request,
        CancellationToken cancellationToken)
    {
        var ledger = await _dbContext.RewardLedger
            .FirstOrDefaultAsync(l => l.Id == request.LedgerId, cancellationToken);

        if (ledger is null)
        {
            return Result.Failure(ReferralErrors.Ledger.NotFound);
        }

        var cancelResult = ledger.Cancel(request.Reason);
        if (cancelResult.IsFailure)
        {
            return cancelResult;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
