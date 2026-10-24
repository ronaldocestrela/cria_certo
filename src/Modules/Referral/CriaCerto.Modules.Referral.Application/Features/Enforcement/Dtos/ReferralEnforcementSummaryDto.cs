namespace CriaCerto.Modules.Referral.Application.Features.Enforcement.Dtos;

public sealed record ReferralEnforcementSummaryDto(
    int TotalEvaluated,
    int ReleasedToAvailable,
    int CancelledDueToRefund,
    int CancelledDueToTenantInsolvency,
    int AttributionsMarkedChurned,
    DateTime ReferenceDateUtc);
