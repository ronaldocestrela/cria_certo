using CriaCerto.Api.Configuration;
using CriaCerto.Modules.Backoffice.Application.Features.DataRetention.Commands;
using CriaCerto.Modules.Tenancy.Application.Features.DataRetention;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CriaCerto.Api.BackgroundServices;

public sealed record DataPurgeConsolidatedExecutionResult(
    TenancyPurgeSummaryDto Tenancy,
    BackofficePurgeSummaryDto Backoffice,
    bool DryRun,
    DateTime ExecutedAtUtc
);

public sealed class DataRetentionPurgingBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<DataRetentionPurgingOptions> _optionsMonitor;
    private readonly ILogger<DataRetentionPurgingBackgroundService> _logger;

    public DataRetentionPurgingBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<DataRetentionPurgingOptions> optionsMonitor,
        ILogger<DataRetentionPurgingBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DataRetentionPurgingBackgroundService iniciado.");

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

            await ProcessDataPurgePassSafelyAsync(cancellationToken: stoppingToken);

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

        _logger.LogInformation("DataRetentionPurgingBackgroundService finalizado com sucesso.");
    }

    public async Task<DataPurgeConsolidatedExecutionResult?> ProcessDataPurgePassSafelyAsync(
        DateTime? referenceDateUtc = null,
        bool? dryRun = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Iniciando rotina de expurgo de tokens, convites e sessões expiradas (LGPD Art. 16).");

            var options = _optionsMonitor.CurrentValue;
            var isDryRun = dryRun ?? options.DryRun;
            var refDate = referenceDateUtc ?? DateTime.UtcNow;

            using var scope = _scopeFactory.CreateScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();

            // 1. Expurgo no Módulo Tenancy (tokens de autenticação e convites de equipe)
            var tenancyCommand = new PurgeExpiredTenancyTokensAndInvitesCommand(
                ReferenceDateUtc: refDate,
                TokenGraceDays: options.TokenGraceDays,
                InviteGraceDays: options.InviteGraceDays,
                DryRun: isDryRun);

            var tenancyResult = await sender.Send(tenancyCommand, cancellationToken);
            if (tenancyResult.IsFailure)
            {
                _logger.LogError("Falha ao executar expurgo no módulo Tenancy: {Error}", tenancyResult.Error.Message);
                return null;
            }

            // 2. Expurgo no Módulo Backoffice (sessões administrativas obsoletas e auditoria forense)
            var backofficeCommand = new PurgeExpiredAdminSessionsCommand(
                ReferenceDateUtc: refDate,
                SessionGraceDays: options.AdminSessionGraceDays,
                DryRun: isDryRun);

            var backofficeResult = await sender.Send(backofficeCommand, cancellationToken);
            if (backofficeResult.IsFailure)
            {
                _logger.LogError("Falha ao executar expurgo no módulo Backoffice: {Error}", backofficeResult.Error.Message);
                return null;
            }

            var consolidated = new DataPurgeConsolidatedExecutionResult(
                Tenancy: tenancyResult.Value,
                Backoffice: backofficeResult.Value,
                DryRun: isDryRun,
                ExecutedAtUtc: DateTime.UtcNow);

            _logger.LogInformation(
                "Expurgo de dados temporários executado: {EmailTokens} tokens de confirmação de e-mail e {ResetTokens} tokens de redefinição limpos; {Invites} convites expirados eliminados; {Sessions} sessões administrativas purgadas (DryRun: {DryRun}).",
                tenancyResult.Value.ClearedEmailConfirmationTokens,
                tenancyResult.Value.ClearedPasswordResetTokens,
                tenancyResult.Value.PurgedTeamInvites,
                backofficeResult.Value.TotalPurged,
                isDryRun);

            return consolidated;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Varredura de expurgo de dados cancelada pelo encerramento da aplicação.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao executar varredura de expurgo e retenção de dados temporários.");
            return null;
        }
    }
}
