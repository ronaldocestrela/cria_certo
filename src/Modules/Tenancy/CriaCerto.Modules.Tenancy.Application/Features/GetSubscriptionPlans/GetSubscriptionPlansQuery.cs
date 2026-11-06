using CriaCerto.BuildingBlocks.Abstractions.Results;
using MediatR;

namespace CriaCerto.Modules.Tenancy.Application.Features.GetSubscriptionPlans;

public record GetSubscriptionPlansQuery : IRequest<Result<List<SubscriptionPlanDto>>>;

public record GetModularSubscriptionCatalogQuery : IRequest<Result<ModularSubscriptionCatalogDto>>;

public sealed class GetSubscriptionPlansQueryHandler :
    IRequestHandler<GetSubscriptionPlansQuery, Result<List<SubscriptionPlanDto>>>,
    IRequestHandler<GetModularSubscriptionCatalogQuery, Result<ModularSubscriptionCatalogDto>>
{
    private readonly ISubscriptionPlansProvider? _plansProvider;

    public GetSubscriptionPlansQueryHandler(ISubscriptionPlansProvider? plansProvider = null)
    {
        _plansProvider = plansProvider;
    }

    public async Task<Result<ModularSubscriptionCatalogDto>> Handle(GetModularSubscriptionCatalogQuery request, CancellationToken cancellationToken)
    {
        if (_plansProvider != null)
        {
            var catalogFromProvider = await _plansProvider.GetModularCatalogAsync(cancellationToken);
            if (catalogFromProvider is { HeadTiers.Count: > 0, Modules.Count: > 0 })
            {
                return Result.Success(catalogFromProvider);
            }
        }

        return Result.Success(GetDefaultModularCatalog());
    }

    public static ModularSubscriptionCatalogDto GetDefaultModularCatalog()
    {
        var tiers = new List<HeadTierOptionDto>
        {
            new("TIER_500", "Até 500 cabeças", 1, 500, 1),
            new("TIER_1500", "De 501 a 1.500 cabeças", 501, 1500, 2),
            new("TIER_3000", "De 1.501 a 3.000 cabeças", 1501, 3000, 3),
            new("TIER_5000_PLUS", "Acima de 3.000 cabeças", 3001, 10000, 4)
        };

        static IReadOnlyList<ModuleTierPriceOptionDto> BuildPrices(string moduleKey, decimal p500, decimal p1500, decimal p3000, decimal p5000)
        {
            var k = moduleKey.ToLowerInvariant();
            return
            [
                new("TIER_500", p500, Math.Round(p500 * 0.80m, 2), $"price_{k}_tier_500_monthly", $"price_{k}_tier_500_annual"),
                new("TIER_1500", p1500, Math.Round(p1500 * 0.80m, 2), $"price_{k}_tier_1500_monthly", $"price_{k}_tier_1500_annual"),
                new("TIER_3000", p3000, Math.Round(p3000 * 0.80m, 2), $"price_{k}_tier_3000_monthly", $"price_{k}_tier_3000_annual"),
                new("TIER_5000_PLUS", p5000, Math.Round(p5000 * 0.80m, 2), $"price_{k}_tier_5000_plus_monthly", $"price_{k}_tier_5000_plus_annual")
            ];
        }

        var modules = new List<CommercialModuleOptionDto>
        {
            new("Cows", "Gestão do Plantel (Base)", "Gestão cadastral do rebanho, lotes básicos, unidades produtivas e dashboard executivo.", "pets", true, 1, BuildPrices("Cows", 79m, 139m, 219m, 349m)),
            new("Breeding", "Reprodução & IATF", "Protocolos de IATF, inseminação artificial, monta natural e diagnóstico de gestação.", "favorite", false, 2, BuildPrices("Breeding", 49m, 89m, 139m, 199m)),
            new("Calving", "Maternidade & Bezerreiro", "Registro de partos, identificação de bezerros, desmama e peso ajustado aos 205 dias.", "child_care", false, 3, BuildPrices("Calving", 39m, 69m, 109m, 159m)),
            new("Growth", "Recria, Engorda & Pastos", "Pesagens, cálculo de GPD/@, movimentação de lotes e taxa de lotação de pastos (UA/ha).", "trending_up", false, 4, BuildPrices("Growth", 59m, 99m, 159m, 229m)),
            new("Sanitary", "Sanitário & Carência", "Campanhas de vacinação, vermifugação, tratamentos veterinários e bloqueio de carência de abate.", "vaccines", false, 5, BuildPrices("Sanitary", 49m, 89m, 139m, 199m)),
            new("Nutrition", "Nutrição & Confinamento", "Suplementação a pasto, estoque de silos, carregamento de trato TMR e conversão alimentar.", "restaurant", false, 6, BuildPrices("Nutrition", 79m, 149m, 239m, 349m)),
            new("Analytics", "BI & Analytics Avançado", "Relatórios zootécnicos avançados, inteligência de rebanho e indicadores executivos.", "insights", false, 7, BuildPrices("Analytics", 49m, 79m, 129m, 189m))
        };

        return new ModularSubscriptionCatalogDto(tiers, modules);
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
                IncludedModules: new[] { "Cows", "Breeding", "Calving" },
                IsPopular: false,
                Features: new List<SubscriptionPlanFeatureDto>
                {
                    new("Modules.Cows", "Módulo de Plantel Bovino & Matrizes", true),
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
                IncludedModules: new[] { "Cows", "Breeding", "Calving", "Growth", "Nutrition", "Sanitary" },
                IsPopular: true,
                Features: new List<SubscriptionPlanFeatureDto>
                {
                    new("Modules.Cows", "Módulo de Plantel Bovino & Matrizes", true),
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
                IncludedModules: new[] { "Cows", "Breeding", "Calving", "Growth", "Nutrition", "Sanitary", "Feedlot", "Analytics" },
                IsPopular: false,
                Features: new List<SubscriptionPlanFeatureDto>
                {
                    new("Modules.Cows", "Módulo de Plantel Bovino & Matrizes", true),
                    new("Modules.Breeding", "Módulo de Reprodução & IATF", true),
                    new("Modules.Calving", "Módulo de Partos & Bezerreiro", true),
                    new("Modules.Growth", "Módulo de Manejo & Pesagem", true),
                    new("Modules.Sanitary", "Módulo Sanitário & Vacinação", true),
                    new("Modules.Nutrition", "Módulo Nutricional & Suplementação", true),
                    new("Modules.Feedlot", "Módulo de Confinamento & Trato TMR", true),
                    new("Modules.Analytics", "Zootecnia Avançada & Analytics", true),
                    new("PwaOfflineMode", "Modo Offline PWA em Curral", true)
                }
            )
        };

        return Result.Success(fallbackPlans);
    }
}
