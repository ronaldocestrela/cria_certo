namespace CriaCerto.Modules.Tenancy.Application.Options;

public sealed class SubscriptionLifecycleOptions
{
    public const string SectionName = "SubscriptionLifecycle";

    public int IntervalHours { get; set; } = 6;
    public int PastDueGracePeriodDays { get; set; } = 7;
    public int BatchSize { get; set; } = 100;
}
