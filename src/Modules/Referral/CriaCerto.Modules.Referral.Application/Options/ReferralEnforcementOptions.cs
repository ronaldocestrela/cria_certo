namespace CriaCerto.Modules.Referral.Application.Options;

public sealed class ReferralEnforcementOptions
{
    public const string SectionName = "Referral:Enforcement";

    public int IntervalHours { get; set; } = 24;
    public int InitialDelaySeconds { get; set; } = 20;
}
