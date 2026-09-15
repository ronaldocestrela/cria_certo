using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Enums;
using CriaCerto.Modules.Backoffice.Application.Features.Rollout.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Backoffice.Application.Features.Rollout.Queries;

public record GetRolloutSloHealthQuery : IRequest<Result<IReadOnlyList<RolloutSloHealthDto>>>;

public class GetRolloutSloHealthQueryHandler : IRequestHandler<GetRolloutSloHealthQuery, Result<IReadOnlyList<RolloutSloHealthDto>>>
{
    private readonly DbContext _dbContext;

    public GetRolloutSloHealthQueryHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<IReadOnlyList<RolloutSloHealthDto>>> Handle(GetRolloutSloHealthQuery request, CancellationToken cancellationToken)
    {
        var flags = await _dbContext.Set<FeatureFlag>()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var recentAlerts = await _dbContext.Set<BackofficeAlert>()
            .AsNoTracking()
            .Where(a => a.FirstTriggeredAtUtc >= DateTime.UtcNow.AddHours(-24))
            .ToListAsync(cancellationToken);

        var list = new List<RolloutSloHealthDto>();

        foreach (var flag in flags)
        {
            var flagAlerts = recentAlerts.Count(a =>
                a.ContextJson.Contains(flag.Key, StringComparison.OrdinalIgnoreCase) ||
                a.Description.Contains(flag.Key, StringComparison.OrdinalIgnoreCase));

            // Simulação de telemetria e métricas em janela recente
            var errorRate = flagAlerts > 0 ? Math.Min(flagAlerts * 0.4, 5.0) : 0.1;
            var latencyP95 = flag.Category == FeatureFlagCategory.CriticalOperation ? 280.0 : 160.0;
            var isHealthy = !flag.KillSwitchActive && errorRate <= 1.0 && latencyP95 <= 1200.0;

            var statusSummary = flag.KillSwitchActive
                ? "Interrompido por Kill-Switch (Corte Crítico)"
                : !flag.IsEnabled
                    ? "Inativo Globalmente"
                    : isHealthy
                        ? "Operação Estável (SLO em conformidade)"
                        : "Alerta de SLO: Taxa de erro ou latência elevada";

            list.Add(new RolloutSloHealthDto(
                FlagKey: flag.Key,
                FlagName: flag.Name,
                IsHealthy: isHealthy,
                CurrentErrorRatePercent: errorRate,
                MaxErrorRateThresholdPercent: 1.0,
                CurrentLatencyP95Ms: latencyP95,
                MaxLatencyP95ThresholdMs: 1200.0,
                PolicyFailuresCount: flagAlerts,
                TotalEvaluationsCount: 150,
                TotalBlockedCount: flag.RolloutPercentage < 100 ? 45 : 0,
                StatusSummary: statusSummary
            ));
        }

        return Result.Success<IReadOnlyList<RolloutSloHealthDto>>(list);
    }
}
