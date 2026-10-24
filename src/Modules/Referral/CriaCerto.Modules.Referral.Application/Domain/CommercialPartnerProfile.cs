using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Domain.Errors;

namespace CriaCerto.Modules.Referral.Application.Domain;

public sealed class CommercialPartnerProfile
{
    public Guid Id { get; private set; }
    public Guid ParticipantId { get; private set; }
    public string FullName { get; private set; } = string.Empty;
    public string? TradeName { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string PhoneNumber { get; private set; } = string.Empty;
    public PartnerCategory Category { get; private set; }
    public string? City { get; private set; }
    public string? State { get; private set; }
    public string? Notes { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    private CommercialPartnerProfile() { }

    public static Result<CommercialPartnerProfile> Create(
        Guid participantId,
        string fullName,
        string? tradeName,
        string email,
        string phoneNumber,
        PartnerCategory category,
        string? city = null,
        string? state = null,
        string? notes = null)
    {
        if (participantId == Guid.Empty)
            return Result.Failure<CommercialPartnerProfile>(ReferralErrors.PartnerProfile.ParticipantRequired);

        if (string.IsNullOrWhiteSpace(fullName))
            return Result.Failure<CommercialPartnerProfile>(ReferralErrors.PartnerProfile.FullNameRequired);

        if (string.IsNullOrWhiteSpace(email))
            return Result.Failure<CommercialPartnerProfile>(ReferralErrors.PartnerProfile.EmailRequired);

        if (string.IsNullOrWhiteSpace(phoneNumber))
            return Result.Failure<CommercialPartnerProfile>(ReferralErrors.PartnerProfile.PhoneRequired);

        var profile = new CommercialPartnerProfile
        {
            Id = Guid.NewGuid(),
            ParticipantId = participantId,
            FullName = fullName.Trim(),
            TradeName = string.IsNullOrWhiteSpace(tradeName) ? null : tradeName.Trim(),
            Email = email.Trim().ToLowerInvariant(),
            PhoneNumber = phoneNumber.Trim(),
            Category = category,
            City = string.IsNullOrWhiteSpace(city) ? null : city.Trim(),
            State = string.IsNullOrWhiteSpace(state) ? null : state.Trim().ToUpperInvariant(),
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        return Result.Success(profile);
    }

    public Result UpdateContactInfo(
        string fullName,
        string? tradeName,
        string email,
        string phoneNumber,
        PartnerCategory category,
        string? city,
        string? state,
        string? notes)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return Result.Failure(ReferralErrors.PartnerProfile.FullNameRequired);

        if (string.IsNullOrWhiteSpace(email))
            return Result.Failure(ReferralErrors.PartnerProfile.EmailRequired);

        if (string.IsNullOrWhiteSpace(phoneNumber))
            return Result.Failure(ReferralErrors.PartnerProfile.PhoneRequired);

        FullName = fullName.Trim();
        TradeName = string.IsNullOrWhiteSpace(tradeName) ? null : tradeName.Trim();
        Email = email.Trim().ToLowerInvariant();
        PhoneNumber = phoneNumber.Trim();
        Category = category;
        City = string.IsNullOrWhiteSpace(city) ? null : city.Trim();
        State = string.IsNullOrWhiteSpace(state) ? null : state.Trim().ToUpperInvariant();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        UpdatedAtUtc = DateTime.UtcNow;

        return Result.Success();
    }
}
