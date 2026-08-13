namespace CriaCerto.Web.Client.Models;

public record BackofficeDashboardKpisModel(
    int TotalTenants,
    int ActiveTenants,
    int TrialTenants,
    int PastDueTenants,
    int SuspendedTenants,
    decimal DelinquencyRatePercentage,
    int ActiveSubscriptionsCount,
    decimal MonthlyRecurringRevenue,
    string SystemHealthStatus,
    DateTime CalculatedAtUtc
);
