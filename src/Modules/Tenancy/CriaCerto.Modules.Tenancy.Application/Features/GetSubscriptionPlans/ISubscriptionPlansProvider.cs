namespace CriaCerto.Modules.Tenancy.Application.Features.GetSubscriptionPlans;

public interface ISubscriptionPlansProvider
{
    Task<List<SubscriptionPlanDto>?> GetActivePlansAsync(CancellationToken cancellationToken = default);

    Task<ModularSubscriptionCatalogDto?> GetModularCatalogAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<ModularSubscriptionCatalogDto?>(null);
}
