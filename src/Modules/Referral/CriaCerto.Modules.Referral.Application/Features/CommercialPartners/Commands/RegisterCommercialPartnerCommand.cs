using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.BuildingBlocks.Abstractions.Security;
using CriaCerto.Modules.Referral.Application.Abstractions;
using CriaCerto.Modules.Referral.Application.Domain;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Domain.Errors;
using CriaCerto.Modules.Referral.Application.Domain.Services;
using CriaCerto.Modules.Referral.Application.Features.CommercialPartners.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Referral.Application.Features.CommercialPartners.Commands;

public record RegisterCommercialPartnerCommand(
    string FullName,
    string? TradeName,
    string Email,
    string PhoneNumber,
    PartnerCategory Category,
    string? City,
    string? State,
    string? Notes,
    string LegalDocumentNumber,
    PixKeyType PixKeyType,
    string PixKey,
    CommissionType CommissionType,
    decimal CommissionValue,
    int? MaxRecurringMonths,
    int PaymentTermsDays,
    string? PreferredReferralCode,
    Guid CreatedByAdminId,
    string? CreatedByAdminEmail = null,
    string? IpAddress = null
) : IRequest<Result<CommercialPartnerDto>>;

public sealed class RegisterCommercialPartnerCommandHandler
    : IRequestHandler<RegisterCommercialPartnerCommand, Result<CommercialPartnerDto>>
{
    private readonly IReferralDbContext _dbContext;
    private readonly IPartnerAccountProvisioner _accountProvisioner;
    private readonly IDataEncryptionService _encryptionService;

    public RegisterCommercialPartnerCommandHandler(
        IReferralDbContext dbContext,
        IPartnerAccountProvisioner accountProvisioner,
        IDataEncryptionService? encryptionService = null)
    {
        _dbContext = dbContext;
        _accountProvisioner = accountProvisioner;
        _encryptionService = encryptionService ?? NullDataEncryptionService.Instance;
    }

    public async Task<Result<CommercialPartnerDto>> Handle(
        RegisterCommercialPartnerCommand request,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var normalizedDoc = LegalDocumentValidator.Normalize(request.LegalDocumentNumber);
        var docHash = _encryptionService.ComputeBlindIndex(normalizedDoc);

        // 1. Unicidade de E-mail
        var emailExists = await _dbContext.PartnerProfiles
            .AnyAsync(p => p.Email == normalizedEmail, cancellationToken);
        if (emailExists)
        {
            return Result.Failure<CommercialPartnerDto>(ReferralErrors.PartnerProfile.EmailAlreadyInUse);
        }

        // 2. Unicidade de CPF / CNPJ via Blind Index determinístico
        var docExists = await _dbContext.PartnerContracts
            .AnyAsync(c => c.LegalDocumentNumberHash == docHash, cancellationToken);
        if (docExists)
        {
            return Result.Failure<CommercialPartnerDto>(ReferralErrors.PartnerProfile.DocumentAlreadyInUse);
        }

        // 3. Normalização e Validação do Código de Indicação
        var codeCandidate = !string.IsNullOrWhiteSpace(request.PreferredReferralCode)
            ? ReferralCodeNormalizer.NormalizeSlug(request.PreferredReferralCode)
            : ReferralCodeNormalizer.NormalizeSlug(request.TradeName ?? request.FullName);

        var codeExists = await _dbContext.ReferralCodes
            .AnyAsync(c => c.Code == codeCandidate, cancellationToken);

        if (codeExists)
        {
            if (!string.IsNullOrWhiteSpace(request.PreferredReferralCode))
            {
                return Result.Failure<CommercialPartnerDto>(ReferralErrors.PartnerProfile.CodeAlreadyInUse);
            }

            // Gerar sufixo randômico único se gerado automaticamente
            codeCandidate = $"{codeCandidate}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}";
        }

        // 4. Criação do Participante
        var participantResult = ReferralProgramParticipant.CreateCommercialPartner(request.CreatedByAdminId);
        if (participantResult.IsFailure)
        {
            return Result.Failure<CommercialPartnerDto>(participantResult.Error);
        }
        var participant = participantResult.Value;

        // 5. Criação do Perfil
        var profileResult = CommercialPartnerProfile.Create(
            participant.Id,
            request.FullName,
            request.TradeName,
            normalizedEmail,
            request.PhoneNumber,
            request.Category,
            request.City,
            request.State,
            request.Notes);
        if (profileResult.IsFailure)
        {
            return Result.Failure<CommercialPartnerDto>(profileResult.Error);
        }
        var profile = profileResult.Value;

        // 6. Criação do Contrato Comercial Inicial
        var contractResult = PartnerContract.Create(
            participant.Id,
            request.CommissionType,
            request.CommissionValue,
            request.MaxRecurringMonths,
            request.PaymentTermsDays,
            request.PixKey,
            request.PixKeyType,
            normalizedDoc,
            DateTime.UtcNow,
            docHash);
        if (contractResult.IsFailure)
        {
            return Result.Failure<CommercialPartnerDto>(contractResult.Error);
        }
        var contract = contractResult.Value;

        // 7. Criação do Código de Indicação
        var referralCodeResult = ReferralCode.Create(participant.Id, codeCandidate, "Código Principal");
        if (referralCodeResult.IsFailure)
        {
            return Result.Failure<CommercialPartnerDto>(referralCodeResult.Error);
        }
        var referralCode = referralCodeResult.Value;

        // 8. Provisionamento de Acesso Identity
        var provisionResult = await _accountProvisioner.ProvisionPartnerUserAsync(
            request.FullName,
            normalizedEmail,
            request.PhoneNumber,
            referralCode.Code,
            cancellationToken);

        if (provisionResult.IsFailure)
        {
            return Result.Failure<CommercialPartnerDto>(provisionResult.Error);
        }

        if (provisionResult.Value == Guid.Empty)
        {
            return Result.Failure<CommercialPartnerDto>(ReferralErrors.PartnerProfile.UserProvisioningFailed);
        }

        participant.LinkUserAccount(provisionResult.Value);

        // 9. Persistência Atômica no Banco de Dados
        _dbContext.Participants.Add(participant);
        _dbContext.PartnerProfiles.Add(profile);
        _dbContext.PartnerContracts.Add(contract);
        _dbContext.ReferralCodes.Add(referralCode);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var history = new[]
        {
            new PartnerContractHistoryDto(
                contract.Id,
                contract.CommissionType,
                contract.CommissionValue,
                contract.MaxRecurringMonths,
                contract.PaymentTermsDays,
                contract.PixKey,
                contract.PixKeyType,
                contract.LegalDocumentNumber,
                contract.ValidFromUtc,
                contract.ValidToUtc,
                contract.IsActive,
                contract.CreatedAtUtc)
        };

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
            contract.LegalDocumentNumber,
            contract.PixKeyType,
            contract.PixKey,
            contract.CommissionType,
            contract.CommissionValue,
            contract.MaxRecurringMonths,
            contract.PaymentTermsDays,
            referralCode.Code,
            referralCode.ClicksCount,
            profile.CreatedAtUtc,
            profile.UpdatedAtUtc,
            history);

        return Result.Success(dto);
    }
}
