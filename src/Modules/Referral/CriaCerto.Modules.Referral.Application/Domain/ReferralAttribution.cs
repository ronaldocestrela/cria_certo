using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Domain.Errors;

namespace CriaCerto.Modules.Referral.Application.Domain;

public sealed class ReferralAttribution
{
    public Guid Id { get; private set; }
    public Guid ReferralCodeId { get; private set; }
    public Guid ReferrerParticipantId { get; private set; }
    public Guid ReferredTenantId { get; private set; }
    public DateTime AttributionDateUtc { get; private set; }
    public DateTime? ConvertedAtUtc { get; private set; }
    public AttributionStatus Status { get; private set; }
    public string? OriginIpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public string? DeviceFingerprintHash { get; private set; }
    public string? FraudReason { get; private set; }

    private ReferralAttribution() { }

    public static Result<ReferralAttribution> Create(
        Guid referralCodeId,
        Guid referrerParticipantId,
        Guid referredTenantId,
        string? originIpAddress = null,
        string? userAgent = null,
        string? deviceFingerprintHash = null)
    {
        if (referralCodeId == Guid.Empty)
            return Result.Failure<ReferralAttribution>(Error.Validation("ReferralAttribution.CodeRequired", "O código de indicação é obrigatório."));

        if (referrerParticipantId == Guid.Empty)
            return Result.Failure<ReferralAttribution>(Error.Validation("ReferralAttribution.ReferrerRequired", "O participante indicador é obrigatório."));

        if (referredTenantId == Guid.Empty)
            return Result.Failure<ReferralAttribution>(Error.Validation("ReferralAttribution.ReferredTenantRequired", "O tenant indicado é obrigatório."));

        var attribution = new ReferralAttribution
        {
            Id = Guid.NewGuid(),
            ReferralCodeId = referralCodeId,
            ReferrerParticipantId = referrerParticipantId,
            ReferredTenantId = referredTenantId,
            AttributionDateUtc = DateTime.UtcNow,
            ConvertedAtUtc = null,
            Status = AttributionStatus.Pending,
            OriginIpAddress = originIpAddress,
            UserAgent = userAgent,
            DeviceFingerprintHash = deviceFingerprintHash,
            FraudReason = null
        };

        return Result.Success(attribution);
    }

    /// <summary>
    /// Marca a atribuição como Convertida após a confirmação do primeiro pagamento do plano.
    /// Cadastros em Trial permanecem como Pending.
    /// </summary>
    public Result MarkConverted(DateTime paymentDateUtc)
    {
        if (Status == AttributionStatus.Converted)
            return Result.Failure(ReferralErrors.Attribution.AlreadyConverted);

        if (Status == AttributionStatus.Fraudulent)
            return Result.Failure(Error.Conflict("ReferralAttribution.CannotConvertFraudulent", "Atribuições marcadas como fraude não podem ser convertidas."));

        Status = AttributionStatus.Converted;
        ConvertedAtUtc = paymentDateUtc;

        return Result.Success();
    }

    public Result FlagAsFraud(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure(ReferralErrors.Attribution.FraudReasonRequired);

        Status = AttributionStatus.Fraudulent;
        FraudReason = reason.Trim();

        return Result.Success();
    }

    public Result MarkChurned()
    {
        Status = AttributionStatus.Churned;
        return Result.Success();
    }
}
