namespace CriaCerto.Modules.Referral.Application.Abstractions;

public record PlanLookupDto(
    Guid Id,
    string Code,
    string Name,
    bool IsArchived,
    string Category = "HeadTier",
    int SortOrder = 0);

public interface IPlanCatalogLookupService
{
    Task<IReadOnlyList<PlanLookupDto>> GetAvailablePlansAsync(CancellationToken cancellationToken = default);
}
