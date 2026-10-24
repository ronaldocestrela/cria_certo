using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Abstractions;
using CriaCerto.Modules.Referral.Application.Domain;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Domain.Errors;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Referral.Application.Features.CommercialPartners.Commands;

public record UpdateCommercialPartnerProfileCommand(
    Guid ParticipantId,
    string FullName,
    string? TradeName,
    string Email,
    string PhoneNumber,
    PartnerCategory Category,
    string? City,
    string? State,
    string? Notes,
    string? PixKey = null,
    PixKeyType? PixKeyType = null
) : IRequest<Result>;

public sealed class UpdateCommercialPartnerProfileCommandValidator
    : AbstractValidator<UpdateCommercialPartnerProfileCommand>
{
    public UpdateCommercialPartnerProfileCommandValidator()
    {
        RuleFor(x => x.ParticipantId)
            .NotEmpty().WithMessage("O identificador do participante é obrigatório.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("O nome completo ou razão social é obrigatório.")
            .MaximumLength(200);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("O e-mail é obrigatório.")
            .EmailAddress().WithMessage("Formato de e-mail inválido.");

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("O telefone é obrigatório.");

        RuleFor(x => x.Category)
            .IsInEnum().WithMessage("Categoria inválida.");
    }
}

public sealed class UpdateCommercialPartnerProfileCommandHandler
    : IRequestHandler<UpdateCommercialPartnerProfileCommand, Result>
{
    private readonly IReferralDbContext _dbContext;

    public UpdateCommercialPartnerProfileCommandHandler(IReferralDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result> Handle(
        UpdateCommercialPartnerProfileCommand request,
        CancellationToken cancellationToken)
    {
        var profile = await _dbContext.PartnerProfiles
            .FirstOrDefaultAsync(p => p.ParticipantId == request.ParticipantId, cancellationToken);

        if (profile is null)
        {
            return Result.Failure(ReferralErrors.PartnerProfile.NotFound);
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        // Check uniqueness if email changed
        if (!string.Equals(profile.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
        {
            var emailExists = await _dbContext.PartnerProfiles
                .AnyAsync(p => p.Email == normalizedEmail && p.Id != profile.Id, cancellationToken);

            if (emailExists)
            {
                return Result.Failure(ReferralErrors.PartnerProfile.EmailAlreadyInUse);
            }
        }

        var updateResult = profile.UpdateContactInfo(
            request.FullName,
            request.TradeName,
            normalizedEmail,
            request.PhoneNumber,
            request.Category,
            request.City,
            request.State,
            request.Notes);

        if (updateResult.IsFailure)
        {
            return updateResult;
        }

        // If Pix key update is passed, update active contract
        if (!string.IsNullOrWhiteSpace(request.PixKey))
        {
            var activeContract = await _dbContext.PartnerContracts
                .Where(c => c.ParticipantId == request.ParticipantId && c.IsActive)
                .OrderByDescending(c => c.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            if (activeContract is not null)
            {
                // We recreate or replace the contract with the new PixKey
                var closeDate = DateTime.UtcNow;
                activeContract.CloseContract(closeDate);

                var newContract = PartnerContract.Create(
                    request.ParticipantId,
                    activeContract.CommissionType,
                    activeContract.CommissionValue,
                    activeContract.MaxRecurringMonths,
                    activeContract.PaymentTermsDays,
                    request.PixKey.Trim(),
                    request.PixKeyType ?? activeContract.PixKeyType,
                    activeContract.LegalDocumentNumber,
                    closeDate).Value;

                _dbContext.PartnerContracts.Add(newContract);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
