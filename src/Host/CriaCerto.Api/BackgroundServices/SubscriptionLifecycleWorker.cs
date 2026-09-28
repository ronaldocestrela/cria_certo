using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CriaCerto.Api.BackgroundServices;

public sealed class SubscriptionLifecycleWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<SubscriptionLifecycleOptions> _optionsMonitor;
    private readonly ILogger<SubscriptionLifecycleWorker> _logger;

    public SubscriptionLifecycleWorker(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<SubscriptionLifecycleOptions> optionsMonitor,
        ILogger<SubscriptionLifecycleWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SubscriptionLifecycleWorker iniciado.");

        // Breve atraso inicial para assegurar que as migrações e seeders de inicialização estejam concluídos
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var options = _optionsMonitor.CurrentValue;
            var interval = options.IntervalHours > 0
                ? TimeSpan.FromHours(options.IntervalHours)
                : TimeSpan.FromHours(6);

            await ProcessLifecyclePassSafelyAsync(stoppingToken);

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

        _logger.LogInformation("SubscriptionLifecycleWorker finalizado com sucesso.");
    }

    public async Task<SubscriptionLifecycleExecutionResult?> ProcessLifecyclePassSafelyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Iniciando varredura periódica do ciclo de vida de assinaturas.");

            using var scope = _scopeFactory.CreateScope();
            var lifecycleService = scope.ServiceProvider.GetRequiredService<ISubscriptionLifecycleService>();

            var result = await lifecycleService.ExecutePassAsync(cancellationToken);

            if (result.IsSuccess)
            {
                var summary = result.Value;
                _logger.LogInformation(
                    "Ciclo de vida de assinaturas executado: {Total} avaliados, {Trials} trials suspensos, {PastDue} inadimplentes suspensos, {Protected} protegidos preservados.",
                    summary.TotalEvaluated,
                    summary.SuspendedTrials,
                    summary.SuspendedPastDue,
                    summary.ProtectedSkipped);

                return summary;
            }

            _logger.LogError("Falha ao executar ciclo de vida de assinaturas: {Error}", result.Error.Message);
            return null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Varredura de ciclo de vida cancelada pelo encerramento da aplicação.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao executar varredura do ciclo de vida de assinaturas.");
            return null;
        }
    }
}
