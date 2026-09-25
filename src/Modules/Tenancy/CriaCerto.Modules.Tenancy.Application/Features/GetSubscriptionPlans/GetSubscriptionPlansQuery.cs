using CriaCerto.BuildingBlocks.Abstractions.Results;
using MediatR;

namespace CriaCerto.Modules.Tenancy.Application.Features.GetSubscriptionPlans;

public record GetSubscriptionPlansQuery : IRequest<Result<List<SubscriptionPlanDto>>>;

public sealed class GetSubscriptionPlansQueryHandler : IRequestHandler<GetSubscriptionPlansQuery, Result<List<SubscriptionPlanDto>>>
{
    private readonly ISubscriptionPlansProvider? _plansProvider;

    public GetSubscriptionPlansQueryHandler(ISubscriptionPlansProvider? plansProvider = null)
    {
        _plansProvider = plansProvider;
    }

    public async Task<Result<List<SubscriptionPlanDto>>> Handle(GetSubscriptionPlansQuery request, CancellationToken cancellationToken)
    {
        if (_plansProvider != null)
        {
            var plansFromProvider = await _plansProvider.GetActivePlansAsync(cancellationToken);
            if (plansFromProvider is { Count: > 0 })
            {
                return Result.Success(plansFromProvider);
            }
        }

        var fallbackPlans = new List<SubscriptionPlanDto>
        {
            new(
                PlanId: "Starter",
                Name: "Starter Pecuária",
                Description: "Ideal para pequenas propriedades iniciando o controle de plantel e reprodução.",
                MonthlyPrice: 149.00m,
                AnnualPriceMonthly: 119.00m,
                HeadCapacityLimit: 500,
                IncludedModules: new[] { "Breeding", "Calving" },
                IsPopular: false,
                Features: new List<SubscriptionPlanFeatureDto>
                {
                    new("Modules.Breeding", "Módulo de Reprodução & IATF", true),
                    new("Modules.Calving", "Módulo de Partos & Bezerreiro", true),
                    new("PwaOfflineMode", "Modo Offline PWA em Curral", true)
                }
            ),
            new(
                PlanId: "Pro",
                Name: "Pro Fazenda",
                Description: "Gestão completa de pasto, balança de curral, manejo reprodutivo e sanidade.",
                MonthlyPrice: 349.00m,
                AnnualPriceMonthly: 279.00m,
                HeadCapacityLimit: 2500,
                IncludedModules: new[] { "Breeding", "Calving", "Growth", "Nutrition", "Sanitary" },
                IsPopular: true,
                Features: new List<SubscriptionPlanFeatureDto>
                {
                    new("Modules.Breeding", "Módulo de Reprodução & IATF", true),
                    new("Modules.Calving", "Módulo de Partos & Bezerreiro", true),
                    new("Modules.Growth", "Módulo de Manejo & Pesagem", true),
                    new("Modules.Sanitary", "Módulo Sanitário & Vacinação", true),
                    new("Modules.Nutrition", "Módulo Nutricional & Suplementação", true),
                    new("PwaOfflineMode", "Modo Offline PWA em Curral", true)
                }
            ),
            new(
                PlanId: "Enterprise",
                Name: "Enterprise Confinamento",
                Description: "Para grandes grupos pecuários, confinamentos e análise avançada de custo por @.",
                MonthlyPrice: 799.00m,
                AnnualPriceMonthly: 649.00m,
                HeadCapacityLimit: int.MaxValue,
                IncludedModules: new[] { "Breeding", "Calving", "Growth", "Nutrition", "Sanitary", "Analytics" },
                IsPopular: false,
                Features: new List<SubscriptionPlanFeatureDto>
                {
                    new("Modules.Breeding", "Módulo de Reprodução & IATF", true),
                    new("Modules.Calving", "Módulo de Partos & Bezerreiro", true),
                    new("Modules.Growth", "Módulo de Manejo & Pesagem", true),
                    new("Modules.Sanitary", "Módulo Sanitário & Vacinação", true),
                    new("Modules.Nutrition", "Módulo Nutricional & Suplementação", true),
                    new("Modules.Analytics", "Zootecnia Avançada & Analytics", true),
                    new("PwaOfflineMode", "Modo Offline PWA em Curral", true)
                }
            )
        };

        return Result.Success(fallbackPlans);
    }
}
