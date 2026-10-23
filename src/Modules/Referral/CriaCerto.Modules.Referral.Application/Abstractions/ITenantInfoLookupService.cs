namespace CriaCerto.Modules.Referral.Application.Abstractions;

public record TenantSummaryDto(
    Guid TenantId,
    string Name,
    string? TradeName,
    string Status,
    string? PlanCode,
    string? PlanName,
    string? CnpjNormalized = null,
    string? TechnicalOwnerEmail = null,
    string? CommercialOwnerEmail = null,
    string? StripeCustomerId = null,
    decimal? PlanBasePrice = null);

public record TenantsRetentionMetricsDto(
    double ReferredAverageLifespanDays,
    double GlobalAverageLifespanDays,
    decimal ReferredRetentionRatePercentage,
    decimal GlobalRetentionRatePercentage);

public interface ITenantInfoLookupService
{
    Task<TenantSummaryDto?> GetTenantSummaryAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, TenantSummaryDto>> GetTenantSummariesAsync(IEnumerable<Guid> tenantIds, CancellationToken cancellationToken = default);
    Task<int> GetActiveTenantsCountAsync(CancellationToken cancellationToken = default);
    Task<TenantsRetentionMetricsDto> GetTenantsRetentionMetricsAsync(IEnumerable<Guid> referredTenantIds, CancellationToken cancellationToken = default);
}
