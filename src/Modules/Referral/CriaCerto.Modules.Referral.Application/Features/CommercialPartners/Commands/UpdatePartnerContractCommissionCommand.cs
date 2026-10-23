using System.Text.Json;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Abstractions;
using CriaCerto.Modules.Referral.Application.Domain;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Domain.Errors;
using CriaCerto.Modules.Referral.Application.Features.CommercialPartners.Dtos;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Referral.Application.Features.CommercialPartners.Commands;

public record UpdatePartnerContractCommissionCommand(
    Guid ParticipantId,
    CommissionType CommissionType,
    decimal CommissionValue,
    int? MaxRecurringMonths,
    int PaymentTermsDays,
    string? PixKey,
    PixKeyType? PixKeyType,
    Guid AdminUserId,
    string? Justification = null
) : IRequest<Result<CommercialPartnerDto>>;

public sealed class UpdatePartnerContractCommissionCommandValidator
    : AbstractValidator<UpdatePartnerContractCommissionCommand>
{
    public UpdatePartnerContractCommissionCommandValidator()
    {
        RuleFor(x => x.ParticipantId)
            .NotEmpty().WithMessage("O identificador do participante é obrigatório.");

        RuleFor(x => x.CommissionType)
            .IsInEnum().WithMessage("O modelo de comissionamento é inválido.");

        When(x => x.CommissionType is CommissionType.PercentageRecurring or CommissionType.PercentageFirstMonth, () =>
        {
            RuleFor(x => x.CommissionValue)
                .GreaterThan(0).WithMessage("A porcentagem de comissão deve ser maior que zero.")
                .LessThanOrEqualTo(100).WithMessage("A porcentagem de comissão não pode exceder 100%.");
        });

        When(x => x.CommissionType == CommissionType.FixedPerConversion, () =>
        {
            RuleFor(x => x.CommissionValue)
                .GreaterThan(0).WithMessage("O valor fixo por conversão deve ser maior que zero.");
        });

        RuleFor(x => x.PaymentTermsDays)
            .GreaterThan(0).WithMessage("O prazo de carência para repasse deve ser maior que zero.");

        RuleFor(x => x.AdminUserId)
            .NotEmpty().WithMessage("O identificador do administrador responsável é obrigatório.");
    }
}

public sealed class UpdatePartnerContractCommissionCommandHandler
    : IRequestHandler<UpdatePartnerContractCommissionCommand, Result<CommercialPartnerDto>>
{
    private readonly IReferralDbContext _dbContext;
    private readonly IReferralAuditSink? _auditSink;

    public UpdatePartnerContractCommissionCommandHandler(
        IReferralDbContext dbContext,
        IReferralAuditSink? auditSink = null)
    {
        _dbContext = dbContext;
        _auditSink = auditSink;
    }

    public async Task<Result<CommercialPartnerDto>> Handle(
        UpdatePartnerContractCommissionCommand request,
        CancellationToken cancellationToken)
    {
        var participant = await _dbContext.Participants
            .FirstOrDefaultAsync(p => p.Id == request.ParticipantId, cancellationToken);

        if (participant is null)
        {
            return Result.Failure<CommercialPartnerDto>(ReferralErrors.PartnerProfile.NotFound);
        }

        var profile = await _dbContext.PartnerProfiles
            .FirstOrDefaultAsync(p => p.ParticipantId == request.ParticipantId, cancellationToken);

        if (profile is null)
        {
            return Result.Failure<CommercialPartnerDto>(ReferralErrors.PartnerProfile.NotFound);
        }

        var currentContract = await _dbContext.PartnerContracts
            .Where(c => c.ParticipantId == request.ParticipantId && c.IsActive)
            .OrderByDescending(c => c.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var legalDocument = currentContract?.LegalDocumentNumber ?? string.Empty;
        var pixKey = !string.IsNullOrWhiteSpace(request.PixKey) ? request.PixKey : (currentContract?.PixKey ?? string.Empty);
        var pixKeyType = request.PixKeyType ?? (currentContract?.PixKeyType ?? PixKeyType.RandomKey);

        // Encerra contrato vigente anterior (Vigência com Efeito Global Imediato)
        var closeDate = DateTime.UtcNow;
        if (currentContract is not null)
        {
            var closeResult = currentContract.CloseContract(closeDate);
            if (closeResult.IsFailure)
            {
                return Result.Failure<CommercialPartnerDto>(closeResult.Error);
            }
        }

        // Abre nova versão do contrato
        var newContractResult = PartnerContract.Create(
            participant.Id,
            request.CommissionType,
            request.CommissionValue,
            request.MaxRecurringMonths,
            request.PaymentTermsDays,
            pixKey,
            pixKeyType,
            legalDocument,
            closeDate);

        if (newContractResult.IsFailure)
        {
            return Result.Failure<CommercialPartnerDto>(newContractResult.Error);
        }

        var newContract = newContractResult.Value;
        _dbContext.PartnerContracts.Add(newContract);

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Registro de Auditoria Forense com encadeamento de hash SHA-256 no Backoffice
        if (_auditSink is not null)
        {
            var oldValues = currentContract is not null ? new
            {
                ContractId = currentContract.Id,
                CommissionType = currentContract.CommissionType.ToString(),
                currentContract.CommissionValue,
                currentContract.MaxRecurringMonths,
                currentContract.PaymentTermsDays,
                currentContract.ValidFromUtc,
                ValidToUtc = closeDate
            } : null;

            var newValues = new
            {
                ContractId = newContract.Id,
                CommissionType = newContract.CommissionType.ToString(),
                newContract.CommissionValue,
                newContract.MaxRecurringMonths,
                newContract.PaymentTermsDays,
                newContract.ValidFromUtc,
                AdminUserId = request.AdminUserId,
                Justification = request.Justification
            };

            var detailsJson = JsonSerializer.Serialize(new
            {
                Action = "PartnerContractCommissionUpdated",
                ParticipantId = participant.Id,
                PartnerFullName = profile.FullName,
                OldContract = oldValues,
                NewContract = newValues
            });

            await _auditSink.RecordSystemAuditAsync(
                action: "Referral.PartnerContract.CommissionUpdated",
                resource: $"PartnerContract/{newContract.Id}",
                targetTenantId: null,
                detailsJson: detailsJson,
                cancellationToken: cancellationToken);
        }

        var referralCode = await _dbContext.ReferralCodes
            .Where(c => c.ParticipantId == request.ParticipantId && c.IsActive)
            .FirstOrDefaultAsync(cancellationToken);

        var allContracts = await _dbContext.PartnerContracts
            .Where(c => c.ParticipantId == request.ParticipantId)
            .OrderByDescending(c => c.CreatedAtUtc)
            .Select(c => new PartnerContractHistoryDto(
                c.Id,
                c.CommissionType,
                c.CommissionValue,
                c.MaxRecurringMonths,
                c.PaymentTermsDays,
                c.PixKey,
                c.PixKeyType,
                c.LegalDocumentNumber,
                c.ValidFromUtc,
                c.ValidToUtc,
                c.IsActive,
                c.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        var dto = new CommercialPartnerDto(
            profile.Id,
            participant.Id,
            participant.UserId,
            profile.FullName,
            profile.TradeName,
            profile.Email,
            profile.PhoneNumber,
            profile.Category,
            profile.City,
            profile.State,
            profile.Notes,
            participant.Status,
            participant.SuspensionReason,
            newContract.LegalDocumentNumber,
            newContract.PixKeyType,
            newContract.PixKey,
            newContract.CommissionType,
            newContract.CommissionValue,
            newContract.MaxRecurringMonths,
            newContract.PaymentTermsDays,
            referralCode?.Code ?? string.Empty,
            referralCode?.ClicksCount ?? 0,
            profile.CreatedAtUtc,
            profile.UpdatedAtUtc,
            allContracts);

        return Result.Success(dto);
    }
}
