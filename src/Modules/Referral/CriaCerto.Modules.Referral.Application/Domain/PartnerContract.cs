using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Domain.Errors;

namespace CriaCerto.Modules.Referral.Application.Domain;

public sealed class PartnerContract
{
    public Guid Id { get; private set; }
    public Guid ParticipantId { get; private set; }
    public CommissionType CommissionType { get; private set; }
    public decimal CommissionValue { get; private set; }
    public int? MaxRecurringMonths { get; private set; }
    public int PaymentTermsDays { get; private set; }
    public string PixKey { get; private set; } = string.Empty;
    public PixKeyType PixKeyType { get; private set; }
    public string LegalDocumentNumber { get; private set; } = string.Empty;
    public DateTime ValidFromUtc { get; private set; }
    public DateTime? ValidToUtc { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private PartnerContract() { }

    public static Result<PartnerContract> Create(
        Guid participantId,
        CommissionType commissionType,
        decimal commissionValue,
        int? maxRecurringMonths,
        int paymentTermsDays,
        string pixKey,
        PixKeyType pixKeyType,
        string legalDocumentNumber,
        DateTime validFromUtc)
    {
        if (participantId == Guid.Empty)
            return Result.Failure<PartnerContract>(Error.Validation("PartnerContract.ParticipantRequired", "O participante é obrigatório."));

        if (string.IsNullOrWhiteSpace(legalDocumentNumber))
            return Result.Failure<PartnerContract>(ReferralErrors.Contract.LegalDocumentRequired);

        if (string.IsNullOrWhiteSpace(pixKey))
            return Result.Failure<PartnerContract>(ReferralErrors.Contract.PixKeyRequired);

        if (commissionType is CommissionType.PercentageRecurring or CommissionType.PercentageFirstMonth)
        {
            if (commissionValue <= 0 || commissionValue > 100)
                return Result.Failure<PartnerContract>(ReferralErrors.Contract.InvalidPercentageRate);
        }
        else if (commissionType == CommissionType.FixedPerConversion)
        {
            if (commissionValue <= 0)
                return Result.Failure<PartnerContract>(ReferralErrors.Contract.InvalidFixedAmount);
        }

        var contract = new PartnerContract
        {
            Id = Guid.NewGuid(),
            ParticipantId = participantId,
            CommissionType = commissionType,
            CommissionValue = commissionValue,
            MaxRecurringMonths = maxRecurringMonths,
            PaymentTermsDays = paymentTermsDays <= 0 ? 30 : paymentTermsDays,
            PixKey = pixKey.Trim(),
            PixKeyType = pixKeyType,
            LegalDocumentNumber = legalDocumentNumber.Trim(),
            ValidFromUtc = validFromUtc,
            ValidToUtc = null,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        return Result.Success(contract);
    }

    public Result CloseContract(DateTime validToUtc)
    {
        if (validToUtc < ValidFromUtc)
            return Result.Failure(ReferralErrors.Contract.InvalidCloseDate);

        ValidToUtc = validToUtc;
        IsActive = false;

        return Result.Success();
    }

    public Result UpdatePixKey(PixKeyType pixKeyType, string pixKey)
    {
        if (string.IsNullOrWhiteSpace(pixKey))
            return Result.Failure(ReferralErrors.Contract.PixKeyRequired);

        PixKeyType = pixKeyType;
        PixKey = pixKey.Trim();

        return Result.Success();
    }
}
