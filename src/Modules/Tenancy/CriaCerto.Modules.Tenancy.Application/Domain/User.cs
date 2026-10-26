using CriaCerto.BuildingBlocks.Abstractions.Results;

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
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? TermsAcceptedAtUtc { get; set; }
    public DateTime? PrivacyPolicyAcceptedAtUtc { get; set; }
    public string? TermsVersion { get; set; }
    public string? PrivacyPolicyVersion { get; set; }
    public string? ConsentIpAddress { get; set; }
    public bool IsAnonymized { get; set; }
    public DateTime? AnonymizedAtUtc { get; set; }
    public string? AnonymizationReason { get; set; }
    public List<UserTenant> UserTenants { get; set; } = new();

    public Result UpdateProfile(string fullName, string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return Result.Failure(Error.Validation("User.InvalidFullName", "O nome completo é obrigatório."));
        }

        FullName = fullName.Trim();
        PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim();
        return Result.Success();
    }

    public Result ChangePassword(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
        {
            return Result.Failure(Error.Validation("User.InvalidPasswordHash", "O hash da nova senha não pode ser vazio."));
        }

        PasswordHash = newPasswordHash;
        return Result.Success();
    }

    public Result Anonymize(string anonymizedEmail, string? reason = null)
    {
        if (IsAnonymized)
        {
            return Result.Failure(Error.Conflict("User.AlreadyAnonymized", "A conta já se encontra anonimizada e encerrada."));
        }

        if (string.IsNullOrWhiteSpace(anonymizedEmail))
        {
            return Result.Failure(Error.Validation("User.InvalidAnonymizedEmail", "O e-mail de anonimização não pode ser vazio."));
        }

        FullName = "Titular Anonimizado";
        Email = anonymizedEmail.Trim().ToLowerInvariant();
        PhoneNumber = null;
        PasswordHash = "ANONYMIZED_DISABLED_CREDENTIAL";
        EmailConfirmationToken = null;
        EmailConfirmationTokenExpiresAt = null;
        PasswordResetToken = null;
        PasswordResetTokenExpiresAt = null;
        IsAnonymized = true;
        AnonymizedAtUtc = DateTime.UtcNow;
        AnonymizationReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

        return Result.Success();
    }
}

