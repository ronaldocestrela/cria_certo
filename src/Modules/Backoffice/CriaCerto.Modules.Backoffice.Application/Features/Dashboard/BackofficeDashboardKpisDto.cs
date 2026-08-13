namespace CriaCerto.Modules.Backoffice.Application.Features.Dashboard;

public record BackofficeDashboardKpisDto(
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
