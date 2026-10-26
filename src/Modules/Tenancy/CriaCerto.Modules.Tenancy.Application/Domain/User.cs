namespace CriaCerto.Modules.Tenancy.Application.Domain;

public sealed class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public bool IsEmailConfirmed { get; set; }
    public DateTime? EmailConfirmedAtUtc { get; set; }
    public string? EmailConfirmationToken { get; set; }
    public DateTime? EmailConfirmationTokenExpiresAt { get; set; }
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetTokenExpiresAt { get; set; }
    public DateTime? TermsAcceptedAtUtc { get; set; }
    public DateTime? PrivacyPolicyAcceptedAtUtc { get; set; }
    public string? TermsVersion { get; set; }
    public string? PrivacyPolicyVersion { get; set; }
    public string? ConsentIpAddress { get; set; }
    public List<UserTenant> UserTenants { get; set; } = new();
}
