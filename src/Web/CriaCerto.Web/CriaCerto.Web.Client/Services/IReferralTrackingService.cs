namespace CriaCerto.Web.Client.Services;

public record ReferralValidationResult(
    bool IsValid,
    string? Code,
    string? CampaignTitle,
    string? ParticipantType,
    string? ErrorMessage
);

public interface IReferralTrackingService
{
    Task CaptureReferralCodeAsync(string? code);
    Task<string?> GetReferralCodeAsync();
    Task ClearReferralCodeAsync();
    Task<ReferralValidationResult> ValidateReferralCodeAsync(string code);
}
