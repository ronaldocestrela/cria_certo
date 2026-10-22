namespace CriaCerto.Modules.Referral.Application.Abstractions;

public record TenantSummaryDto(
    Guid TenantId,
    string Name,
    string? TradeName,
    string Status,
    string? PlanCode,
    string? PlanName);

public interface ITenantInfoLookupService
{
    Task<TenantSummaryDto?> GetTenantSummaryAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, TenantSummaryDto>> GetTenantSummariesAsync(IEnumerable<Guid> tenantIds, CancellationToken cancellationToken = default);
}
