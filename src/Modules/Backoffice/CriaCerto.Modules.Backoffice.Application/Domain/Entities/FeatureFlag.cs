using System.Text.Json;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Enums;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;

namespace CriaCerto.Modules.Backoffice.Application.Domain.Entities;

public class FeatureFlag
{
    public Guid Id { get; private set; }
    public string Key { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public FeatureFlagCategory Category { get; private set; }
    public bool IsEnabled { get; private set; }
    public int RolloutPercentage { get; private set; }
    public RolloutRing MaxAllowedRing { get; private set; }
    public string AllowedRolesJson { get; private set; } = "[]";
    public string WhitelistedAdminEmailsJson { get; private set; } = "[]";
    public string BlacklistedAdminEmailsJson { get; private set; } = "[]";

    // Kill-Switch / Circuit Breaker
    public bool KillSwitchActive { get; private set; }
    public string? KillSwitchReason { get; private set; }
    public DateTime? KillSwitchActivatedAtUtc { get; private set; }
    public string? KillSwitchActivatedBy { get; private set; }

    // Auditoria e Governança
    public DateTime CreatedAtUtc { get; private set; }
    public string CreatedBy { get; private set; } = default!;
    public DateTime? UpdatedAtUtc { get; private set; }
    public string? UpdatedBy { get; private set; }
    public string? LastToggledReason { get; private set; }

    private FeatureFlag() { }

    public static Result<FeatureFlag> Create(
        string key,
        string name,
        string description,
        FeatureFlagCategory category,
        RolloutRing initialRing = RolloutRing.Ring0_Canary,
        int initialPercentage = 0,
        string createdBy = "system")
    {
        if (string.IsNullOrWhiteSpace(key))
            return Result.Failure<FeatureFlag>(FeatureFlagErrors.KeyRequired);

        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<FeatureFlag>(FeatureFlagErrors.NameRequired);

        if (initialPercentage < 0 || initialPercentage > 100)
            return Result.Failure<FeatureFlag>(FeatureFlagErrors.InvalidRolloutPercentage);

        var flag = new FeatureFlag
        {
            Id = Guid.NewGuid(),
            Key = key.Trim().ToLowerInvariant(),
            Name = name.Trim(),
            Description = description?.Trim() ?? string.Empty,
            Category = category,
            IsEnabled = true,
            RolloutPercentage = initialPercentage,
            MaxAllowedRing = initialRing,
            AllowedRolesJson = "[]",
            WhitelistedAdminEmailsJson = "[]",
            BlacklistedAdminEmailsJson = "[]",
            KillSwitchActive = false,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = createdBy
        };

        return Result.Success(flag);
    }

    public Result Toggle(bool isEnabled, string updatedBy, string? reason = null)
    {
        IsEnabled = isEnabled;
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedBy = updatedBy;
        LastToggledReason = reason;

        return Result.Success();
    }

    public Result UpdateRollout(
        int percentage,
        RolloutRing maxAllowedRing,
        IEnumerable<string>? whitelistedEmails,
        string updatedBy,
        string? reason = null)
    {
        if (percentage < 0 || percentage > 100)
            return Result.Failure(FeatureFlagErrors.InvalidRolloutPercentage);

        RolloutPercentage = percentage;
        MaxAllowedRing = maxAllowedRing;

        if (whitelistedEmails is not null)
        {
            SetWhitelistedEmails(whitelistedEmails);
        }

        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedBy = updatedBy;
        LastToggledReason = reason;

        return Result.Success();
    }

    public Result TriggerKillSwitch(string reason, string activatedBy)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 10)
            return Result.Failure(FeatureFlagErrors.JustificationTooShort);

        KillSwitchActive = true;
        KillSwitchReason = reason.Trim();
        KillSwitchActivatedAtUtc = DateTime.UtcNow;
        KillSwitchActivatedBy = activatedBy;
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedBy = activatedBy;

        return Result.Success();
    }

    public Result DeactivateKillSwitch(string restoredBy, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 10)
            return Result.Failure(FeatureFlagErrors.JustificationTooShort);

        KillSwitchActive = false;
        KillSwitchReason = null;
        KillSwitchActivatedAtUtc = null;
        KillSwitchActivatedBy = null;
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedBy = restoredBy;
        LastToggledReason = reason;

        return Result.Success();
    }

    public void SetWhitelistedEmails(IEnumerable<string> emails)
    {
        var cleaned = emails.Where(e => !string.IsNullOrWhiteSpace(e))
                            .Select(e => e.Trim().ToLowerInvariant())
                            .Distinct()
                            .ToList();

        WhitelistedAdminEmailsJson = JsonSerializer.Serialize(cleaned);
    }

    public IReadOnlyList<string> GetWhitelistedEmails()
    {
        if (string.IsNullOrWhiteSpace(WhitelistedAdminEmailsJson))
            return Array.Empty<string>();

        try
        {
            return JsonSerializer.Deserialize<List<string>>(WhitelistedAdminEmailsJson) ?? new List<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}
