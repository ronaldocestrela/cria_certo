using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Domain.Errors;

namespace CriaCerto.Modules.Referral.Application.Domain;

public sealed class ReferralProgramParticipant
{
    public Guid Id { get; private set; }
    public Guid? TenantId { get; private set; }
    public Guid? UserId { get; private set; }
    public ParticipantType ParticipantType { get; private set; }
    public ParticipantStatus Status { get; private set; }
    public string? SuspensionReason { get; private set; }
    public Guid? CreatedByAdminId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    private ReferralProgramParticipant() { }

    public static Result<ReferralProgramParticipant> CreateCustomer(Guid tenantId, Guid? userId = null)
    {
        if (tenantId == Guid.Empty)
            return Result.Failure<ReferralProgramParticipant>(ReferralErrors.Participant.TenantRequiredForCustomer);

        var participant = new ReferralProgramParticipant
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId == Guid.Empty ? null : userId,
            ParticipantType = ParticipantType.Customer,
            Status = ParticipantStatus.Active,
            CreatedAtUtc = DateTime.UtcNow
        };

        return Result.Success(participant);
    }

    public static Result<ReferralProgramParticipant> CreateCommercialPartner(Guid createdByAdminId, Guid? userId = null)
    {
        if (createdByAdminId == Guid.Empty)
            return Result.Failure<ReferralProgramParticipant>(ReferralErrors.Participant.AdminRequiredForPartner);

        var participant = new ReferralProgramParticipant
        {
            Id = Guid.NewGuid(),
            TenantId = null,
            UserId = userId == Guid.Empty ? null : userId,
            ParticipantType = ParticipantType.CommercialPartner,
            Status = ParticipantStatus.Active,
            CreatedByAdminId = createdByAdminId,
            CreatedAtUtc = DateTime.UtcNow
        };

        return Result.Success(participant);
    }

    public Result Suspend(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure(ReferralErrors.Participant.SuspensionReasonRequired);

        Status = ParticipantStatus.Suspended;
        SuspensionReason = reason.Trim();
        UpdatedAtUtc = DateTime.UtcNow;

        return Result.Success();
    }

    public Result Reactivate()
    {
        Status = ParticipantStatus.Active;
        SuspensionReason = null;
        UpdatedAtUtc = DateTime.UtcNow;

        return Result.Success();
    }

    public void LinkUserAccount(Guid userId)
    {
        if (userId != Guid.Empty)
        {
            UserId = userId;
            UpdatedAtUtc = DateTime.UtcNow;
        }
    }
}
