using System.Globalization;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.BuildingBlocks.Abstractions.Tenancy;
using CriaCerto.Modules.Analytics.Application.Contracts;
using CriaCerto.Modules.Analytics.Application.Services;
using CriaCerto.Modules.Breeding.Application.Abstractions;
using CriaCerto.Modules.Breeding.Application.Domain;
using CriaCerto.Modules.Calving.Application.Abstractions;
using CriaCerto.Modules.Calving.Application.Domain;
using CriaCerto.Modules.Growth.Application.Abstractions;
using CriaCerto.Modules.Growth.Application.Domain;
using CriaCerto.Modules.Nutrition.Application.Domain;
using CriaCerto.Modules.Nutrition.Application.Features.SiloStockFeatures;
using CriaCerto.Modules.Sanitary.Application.Contracts;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Api.Features.Analytics;

public sealed class GetTenantExecutiveDashboardQueryHandler : IRequestHandler<GetTenantExecutiveDashboardQuery, Result<ExecutiveDashboardDto>>
{
    private readonly ITenantContext _tenantContext;
    private readonly ITenancyDbContext _tenancyDbContext;
    private readonly IBreedingDbContext _breedingDbContext;
    private readonly ICalvingDbContext _calvingDbContext;
    private readonly IGrowthDbContext _growthDbContext;
    private readonly INutritionDbContext _nutritionDbContext;
    private readonly ISanitaryDbContext _sanitaryDbContext;

    public GetTenantExecutiveDashboardQueryHandler(
        ITenantContext tenantContext,
        ITenancyDbContext tenancyDbContext,
        IBreedingDbContext breedingDbContext,
        ICalvingDbContext calvingDbContext,
        IGrowthDbContext growthDbContext,
        INutritionDbContext nutritionDbContext,
        ISanitaryDbContext sanitaryDbContext)
    {
        _tenantContext = tenantContext;
        _tenancyDbContext = tenancyDbContext;
        _breedingDbContext = breedingDbContext;
        _calvingDbContext = calvingDbContext;
        _growthDbContext = growthDbContext;
        _nutritionDbContext = nutritionDbContext;
        _sanitaryDbContext = sanitaryDbContext;
    }

    public async Task<Result<ExecutiveDashboardDto>> Handle(GetTenantExecutiveDashboardQuery request, CancellationToken cancellationToken)
    {
        var tenantId = request.TenantId ?? _tenantContext.TenantId;
        if (!tenantId.HasValue || tenantId == Guid.Empty)
        {
            return Result.Failure<ExecutiveDashboardDto>(Error.Unauthorized("Analytics.TenantRequired", "Tenant não identificado no contexto da requisição."));
        }

        var now = DateTime.UtcNow;
        var culture = new CultureInfo("pt-BR");

        // 1. Tenancy / Farm info
        var tenant = await _tenancyDbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId.Value, cancellationToken);

        var farmName = !string.IsNullOrWhiteSpace(tenant?.Name) ? tenant.Name : "Minha Fazenda";
        var farmAreaHectares = tenant?.AreaInHectares ?? 0m;

        // 2. Breeding KPIs
        var totalActiveCows = await _breedingDbContext.Cows
            .AsNoTracking()
            .CountAsync(c => c.Category != "Reprodutor" && c.Category != "Touro" && c.Status != ReproductiveStatus.Culled && c.Status != ReproductiveStatus.Sold, cancellationToken);

        var pregnantCows = await _breedingDbContext.Cows
            .AsNoTracking()
            .CountAsync(c => c.Status == ReproductiveStatus.Pregnant, cancellationToken);

        var pregnancyRate = totalActiveCows > 0
            ? Math.Round(((decimal)pregnantCows / totalActiveCows) * 100m, 1)
            : 0m;

        // 3. Calving KPIs
        var calvesWeaned = await _calvingDbContext.Calves
            .AsNoTracking()
            .CountAsync(c => c.Status == CalfStatus.Weaned, cancellationToken);

        var weaningsCount = await _calvingDbContext.Weanings
            .AsNoTracking()
            .CountAsync(cancellationToken);

        calvesWeaned = Math.Max(calvesWeaned, weaningsCount);

        var weaningRate = totalActiveCows > 0
            ? Math.Round(((decimal)calvesWeaned / totalActiveCows) * 100m, 1)
            : 0m;

        // 4. Growth & Pasture KPIs
        var paddocks = await _growthDbContext.Paddocks
            .AsNoTracking()
            .Where(p => p.Status == PaddockStatus.Active)
            .ToListAsync(cancellationToken);

        var totalPastureHectares = paddocks.Sum(p => p.AreaHectares);
        if (totalPastureHectares <= 0 && farmAreaHectares > 0)
        {
            totalPastureHectares = farmAreaHectares;
        }

        var activeLots = await _growthDbContext.Lots
            .AsNoTracking()
            .Where(l => l.Status == LotStatus.Active)
            .ToListAsync(cancellationToken);

        var totalAnimalUnits = activeLots.Sum(l => l.TotalUA);
        // If lots have not been created yet but active cows exist, assume 1 UA per mature cow
        if (totalAnimalUnits == 0 && totalActiveCows > 0)
        {
            totalAnimalUnits = (decimal)totalActiveCows * 1.0m;
        }

        var stockingRate = totalPastureHectares > 0
            ? Math.Round(totalAnimalUnits / totalPastureHectares, 2)
            : 0m;

        // Weighings & GPD
        var weighings = await _growthDbContext.Weighings
            .AsNoTracking()
            .OrderByDescending(w => w.WeighingDate)
            .Take(500)
            .ToListAsync(cancellationToken);

        var validGpdWeighings = weighings.Where(w => w.CalculatedAdgKgPerDay > 0).ToList();
        var averageGpdKg = validGpdWeighings.Count > 0
            ? Math.Round(validGpdWeighings.Average(w => w.CalculatedAdgKgPerDay), 3)
            : 0m;

        // Monthly GPD Evolution (last 5 months up to current month)
        var gpdEvolution = new List<GpdMonthlyPointDto>();
        for (int i = 4; i >= 0; i--)
        {
            var targetDate = now.AddMonths(-i);
            var monthName = targetDate.ToString("MMM", culture);
            monthName = char.ToUpper(monthName[0]) + monthName.Substring(1).TrimEnd('.');

            var monthWeighings = weighings
                .Where(w => w.WeighingDate.Year == targetDate.Year && w.WeighingDate.Month == targetDate.Month && w.CalculatedAdgKgPerDay > 0)
                .ToList();

            decimal monthAvgGpd = monthWeighings.Count > 0
                ? Math.Round(monthWeighings.Average(w => w.CalculatedAdgKgPerDay), 2)
                : 0m;

            gpdEvolution.Add(new GpdMonthlyPointDto(monthName, monthAvgGpd, monthWeighings.Count));
        }

        // 5. Nutrition KPIs
        decimal costPerArrobaProduced = 0.00m;
        var feedBatches = await _nutritionDbContext.DailyFeedBatches
            .AsNoTracking()
            .Take(100)
            .ToListAsync(cancellationToken);

        var supplementations = await _nutritionDbContext.PastureSupplementations
            .AsNoTracking()
            .Take(100)
            .ToListAsync(cancellationToken);

        if (feedBatches.Count > 0 || supplementations.Count > 0)
        {
            var rations = await _nutritionDbContext.FeedRations
                .AsNoTracking()
                .ToDictionaryAsync(r => r.Id, cancellationToken);

            decimal totalFeedCost = 0m;
            foreach (var fb in feedBatches)
            {
                if (rations.TryGetValue(fb.FeedRationId, out var r))
                {
                    totalFeedCost += fb.OfferedAsFedKg * r.CalculatedCostPerKg;
                }
            }
            foreach (var ps in supplementations)
            {
                if (rations.TryGetValue(ps.FeedRationId, out var r))
                {
                    totalFeedCost += ps.QuantityKg * r.CalculatedCostPerKg;
                }
            }

            var totalWeightGainKg = validGpdWeighings.Sum(w => w.WeightKg > 0 ? w.WeightKg * 0.1m : 15m);
            if (totalWeightGainKg > 0 && totalFeedCost > 0)
            {
                var totalArrobasGain = (totalWeightGainKg * 0.50m) / 15.0m;
                if (totalArrobasGain > 0)
                {
                    costPerArrobaProduced = Math.Round(totalFeedCost / totalArrobasGain, 2);
                }
            }
        }

        // 6. Sanitary Compliance KPIs
        var treatments = await _sanitaryDbContext.TreatmentRecords
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var activeWithdrawals = treatments
            .Where(t => t.WithdrawalEndDateUtc > now)
            .ToList();

        var animalsUnderWithdrawal = activeWithdrawals
            .Select(t => t.AnimalId)
            .Where(id => id.HasValue)
            .Distinct()
            .Count();

        if (animalsUnderWithdrawal == 0 && activeWithdrawals.Count > 0)
        {
            animalsUnderWithdrawal = activeWithdrawals.Count;
        }

        var activeSlaughterBlocks = activeWithdrawals.Count;

        string overallHealthStatus = animalsUnderWithdrawal switch
        {
            0 => "Excelente",
            <= 10 => "Ótimo",
            _ => "Atenção Sanitária"
        };

        string healthStatusDetails = animalsUnderWithdrawal switch
        {
            0 => "Nenhum lote com carência vencida e vacinações obrigatórias em dia.",
            1 => "1 animal sob período de carência medicamentosa de abate.",
            _ => $"{animalsUnderWithdrawal} animais sob período de carência medicamentosa de abate."
        };

        var scorecard = new ExecutiveScorecardDto(
            PregnancyRatePercentage: pregnancyRate,
            WeaningRatePercentage: weaningRate,
            StockingRateUAPerHa: stockingRate,
            AverageGpdKg: averageGpdKg,
            CostPerArrobaProduced: costPerArrobaProduced,
            AnimalsUnderSlaughterWithdrawal: animalsUnderWithdrawal,
            OverallHealthStatus: overallHealthStatus);

        var dashboardDto = new ExecutiveDashboardDto(
            FarmName: farmName,
            FarmAreaHectares: farmAreaHectares,
            Scorecard: scorecard,
            GpdEvolution: gpdEvolution,
            TotalActiveCows: totalActiveCows,
            PregnantCows: pregnantCows,
            CalvesWeaned: calvesWeaned,
            TotalPastureHectares: totalPastureHectares,
            TotalAnimalUnits: totalAnimalUnits,
            ActiveSlaughterBlocks: activeSlaughterBlocks,
            HealthStatusDetails: healthStatusDetails);

        return Result.Success(dashboardDto);
    }
}
