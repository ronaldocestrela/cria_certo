namespace CriaCerto.Modules.Tenancy.Application.Features.GetSubscriptionPlans;

public interface ISubscriptionPlansProvider
{
    Task<List<SubscriptionPlanDto>?> GetActivePlansAsync(CancellationToken cancellationToken = default);
}
