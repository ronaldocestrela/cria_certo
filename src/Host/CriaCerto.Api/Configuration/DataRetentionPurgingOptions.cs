namespace CriaCerto.Api.Configuration;

public sealed class DataRetentionPurgingOptions
{
    public const string SectionName = "DataRetention";

    public double IntervalHours { get; set; } = 24;
    public int InitialDelaySeconds { get; set; } = 60;
    public int TokenGraceDays { get; set; } = 7;
    public int InviteGraceDays { get; set; } = 30;
    public int AdminSessionGraceDays { get; set; } = 7;
    public bool DryRun { get; set; } = false;
}
