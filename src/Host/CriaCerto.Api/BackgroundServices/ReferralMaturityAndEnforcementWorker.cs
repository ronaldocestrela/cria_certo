using CriaCerto.Modules.Referral.Application.Abstractions;
using CriaCerto.Modules.Referral.Application.Features.Enforcement.Dtos;
using CriaCerto.Modules.Referral.Application.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CriaCerto.Api.BackgroundServices;

public sealed class ReferralMaturityAndEnforcementWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<ReferralEnforcementOptions> _optionsMonitor;
    private readonly ILogger<ReferralMaturityAndEnforcementWorker> _logger;

    public ReferralMaturityAndEnforcementWorker(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<ReferralEnforcementOptions> optionsMonitor,
        ILogger<ReferralMaturityAndEnforcementWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ReferralMaturityAndEnforcementWorker iniciado.");

        var initialDelaySeconds = _optionsMonitor.CurrentValue.InitialDelaySeconds;
        if (initialDelaySeconds > 0)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(initialDelaySeconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var options = _optionsMonitor.CurrentValue;
            var interval = options.IntervalHours > 0
                ? TimeSpan.FromHours(options.IntervalHours)
                : TimeSpan.FromHours(24);

            await ProcessEnforcementPassSafelyAsync(cancellationToken: stoppingToken);

            try
            {
                using var timer = new PeriodicTimer(interval);
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                {
                    break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("ReferralMaturityAndEnforcementWorker finalizado com sucesso.");
    }

    public async Task<ReferralEnforcementSummaryDto?> ProcessEnforcementPassSafelyAsync(
        DateTime? referenceDateUtc = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Iniciando varredura periódica de maturação e enforcement de comissões de indicação.");

            using var scope = _scopeFactory.CreateScope();
            var enforcementService = scope.ServiceProvider.GetRequiredService<IReferralMaturityEnforcementService>();

            var result = await enforcementService.ExecutePassAsync(referenceDateUtc, cancellationToken);

            if (result.IsSuccess)
            {
                var summary = result.Value;
                _logger.LogInformation(
                    "Ciclo de maturação e enforcement executado: {Total} avaliados, {Released} liberados, {Refunds} estornados, {Insolvent} inadimplentes, {Churned} churns.",
                    summary.TotalEvaluated,
                    summary.ReleasedToAvailable,
                    summary.CancelledDueToRefund,
                    summary.CancelledDueToTenantInsolvency,
                    summary.AttributionsMarkedChurned);

                return summary;
            }

            _logger.LogError("Falha ao executar ciclo de maturação de comissões: {Error}", result.Error.Message);
            return null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Varredura de maturação cancelada pelo encerramento da aplicação.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao executar varredura de maturação e enforcement de comissões.");
            return null;
        }
    }
}
