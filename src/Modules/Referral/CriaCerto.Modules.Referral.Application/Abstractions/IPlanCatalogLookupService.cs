namespace CriaCerto.Modules.Referral.Application.Abstractions;

public record PlanLookupDto(Guid Id, string Code, string Name, bool IsArchived);

public interface IPlanCatalogLookupService
{
    Task<IReadOnlyList<PlanLookupDto>> GetAvailablePlansAsync(CancellationToken cancellationToken = default);
}
