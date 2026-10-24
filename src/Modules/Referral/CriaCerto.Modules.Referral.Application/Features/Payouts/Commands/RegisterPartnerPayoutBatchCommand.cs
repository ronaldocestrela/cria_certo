using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Abstractions;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Domain.Errors;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Referral.Application.Features.Payouts.Commands;

public sealed record RegisterPartnerPayoutBatchCommand(
    Guid PartnerParticipantId,
    IReadOnlyList<Guid> LedgerIds,
    string TransactionReference,
    Stream ReceiptStream,
    string ReceiptFileName,
    string ReceiptContentType,
    Guid RegisteredByAdminId
) : IRequest<Result<int>>;

public sealed class RegisterPartnerPayoutBatchCommandValidator : AbstractValidator<RegisterPartnerPayoutBatchCommand>
{
    public RegisterPartnerPayoutBatchCommandValidator()
    {
        RuleFor(x => x.PartnerParticipantId)
            .NotEmpty().WithMessage("O identificador do parceiro comercial é obrigatório.");

        RuleFor(x => x.LedgerIds)
            .NotNull()
            .Must(x => x != null && x.Count > 0)
            .WithMessage("Ao menos uma comissão liberada deve ser selecionada para o repasse.");

        RuleFor(x => x.TransactionReference)
            .NotEmpty().WithMessage("O código ou identificador de transação bancária / Pix é obrigatório.")
            .MaximumLength(150).WithMessage("O identificador de transação não pode exceder 150 caracteres.");

        RuleFor(x => x.ReceiptFileName)
            .NotEmpty().WithMessage("O nome do arquivo de comprovante é obrigatório.");

        RuleFor(x => x.ReceiptStream)
            .NotNull().WithMessage("O fluxo de dados do comprovante é obrigatório.");
    }
}

public sealed class RegisterPartnerPayoutBatchCommandHandler
    : IRequestHandler<RegisterPartnerPayoutBatchCommand, Result<int>>
{
    private readonly IReferralDbContext _dbContext;
    private readonly IPayoutReceiptStorageService _storageService;

    public RegisterPartnerPayoutBatchCommandHandler(
        IReferralDbContext dbContext,
        IPayoutReceiptStorageService storageService)
    {
        _dbContext = dbContext;
        _storageService = storageService;
    }

    public async Task<Result<int>> Handle(
        RegisterPartnerPayoutBatchCommand request,
        CancellationToken cancellationToken)
    {
        if (request.LedgerIds is null || request.LedgerIds.Count == 0)
        {
            return Result.Failure<int>(ReferralErrors.PayoutBatch.NoCommissionsSelected);
        }

        var partnerExists = await _dbContext.Participants
            .AnyAsync(p => p.Id == request.PartnerParticipantId && p.ParticipantType == ParticipantType.CommercialPartner, cancellationToken);

        if (!partnerExists)
        {
            return Result.Failure<int>(ReferralErrors.PayoutBatch.PartnerNotFound);
        }

        // Save receipt file
        var saveResult = await _storageService.SaveReceiptAsync(
            request.ReceiptStream,
            request.ReceiptFileName,
            request.ReceiptContentType,
            cancellationToken);

        if (saveResult.IsFailure)
        {
            return Result.Failure<int>(saveResult.Error);
        }

        var receiptRelativePath = saveResult.Value;

        // Fetch commissions to pay
        var commissions = await _dbContext.RewardLedger
            .Where(l => request.LedgerIds.Contains(l.Id))
            .ToListAsync(cancellationToken);

        // Validation: all must exist, belong to partner, and be AvailableForPayout
        var allValid = commissions.Count == request.LedgerIds.Count &&
                       commissions.All(c => c.ParticipantId == request.PartnerParticipantId &&
                                           c.Status == LedgerRewardStatus.AvailableForPayout);

        if (!allValid)
        {
            _storageService.DeleteReceipt(receiptRelativePath);
            return Result.Failure<int>(ReferralErrors.PayoutBatch.InvalidCommissionEntry);
        }

        var paidAtUtc = DateTime.UtcNow;

        foreach (var commission in commissions)
        {
            var markResult = commission.MarkPaidOut(
                request.TransactionReference,
                receiptRelativePath,
                request.RegisteredByAdminId,
                paidAtUtc);

            if (markResult.IsFailure)
            {
                _storageService.DeleteReceipt(receiptRelativePath);
                return Result.Failure<int>(markResult.Error);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(commissions.Count);
    }
}
