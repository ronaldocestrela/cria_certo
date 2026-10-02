using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CriaCerto.Modules.Tenancy.Infrastructure.Services;

public sealed class SubscriptionLifecycleService : ISubscriptionLifecycleService
{
    private readonly ITenancyDbContext _dbContext;
    private readonly SubscriptionLifecycleOptions _options;
    private readonly ILogger<SubscriptionLifecycleService> _logger;

    public const string TrialExpiredJustification = "Período de testes expirado.";
    public const string PastDueToleranceExceededJustification = "Inadimplência não regularizada após prazo de tolerância.";
    public const string ActiveExpiredToleranceExceededJustification = "Período de faturamento expirado sem renovação confirmada após prazo de tolerância.";
    public const string ActiveExpiredMarkPastDueJustification = "Período de faturamento expirado sem renovação confirmada.";

    public SubscriptionLifecycleService(
        ITenancyDbContext dbContext,
        IOptions<SubscriptionLifecycleOptions> options,
        ILogger<SubscriptionLifecycleService> logger)
    {
        _dbContext = dbContext;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result<SubscriptionLifecycleExecutionResult>> ExecutePassAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var pastDueGraceDays = _options.PastDueGracePeriodDays > 0 ? _options.PastDueGracePeriodDays : 7;
        var pastDueCutoff = now.AddDays(-pastDueGraceDays);
        var batchSize = _options.BatchSize > 0 ? _options.BatchSize : 100;

        var trialStatus = TenantLifecycle.ToStatusString(TenantStatus.Trial);
        var pastDueStatus = TenantLifecycle.ToStatusString(TenantStatus.PastDue);
        var activeStatus = TenantLifecycle.ToStatusString(TenantStatus.Active);

        var trialCandidates = await _dbContext.Tenants
            .Where(t => t.Status == trialStatus && t.CurrentPeriodEndUtc != null && t.CurrentPeriodEndUtc.Value < now)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        var pastDueCandidates = await _dbContext.Tenants
            .Where(t => t.Status == pastDueStatus &&
                        ((t.StatusChangedAtUtc != null && t.StatusChangedAtUtc.Value <= pastDueCutoff) ||
                         (t.StatusChangedAtUtc == null && t.UpdatedAtUtc <= pastDueCutoff)))
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        var activeExpiredCandidates = await _dbContext.Tenants
            .Where(t => t.Status == activeStatus && t.CurrentPeriodEndUtc != null && t.CurrentPeriodEndUtc.Value < now)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        int totalEvaluated = trialCandidates.Count + pastDueCandidates.Count + activeExpiredCandidates.Count;
        int suspendedTrials = 0;
        int suspendedPastDue = 0;
        int suspendedActive = 0;
        int transitionedToPastDue = 0;
        int protectedSkipped = 0;

        foreach (var tenant in trialCandidates)
        {
            if (tenant.IsProtected)
            {
                _logger.LogWarning("Tentativa de suspender tenant protegido {TenantId} por expiração de trial ignorada (IsProtected=true).", tenant.Id);
                var protHistory = TenantSubscriptionHistory.CreateFromStripeWebhook(
                    tenantId: tenant.Id,
                    actionType: SubscriptionActionType.Suspended,
                    justification: "Tentativa de suspensão automática (período de testes expirado) ignorada devido a IsProtected=true.",
                    snapshotHeadCount: tenant.Capacity
                );
                _dbContext.SubscriptionHistories.Add(protHistory);
                protectedSkipped++;
                continue;
            }

            var suspendResult = tenant.Suspend(TrialExpiredJustification);
            if (suspendResult.IsSuccess)
            {
                var history = TenantSubscriptionHistory.CreateFromStripeWebhook(
                    tenantId: tenant.Id,
                    actionType: SubscriptionActionType.Suspended,
                    justification: TrialExpiredJustification,
                    snapshotHeadCount: tenant.Capacity
                );
                _dbContext.SubscriptionHistories.Add(history);
                suspendedTrials++;
                _logger.LogInformation("Tenant {TenantId} suspenso automaticamente por expiração de período de testes.", tenant.Id);
            }
            else
            {
                _logger.LogWarning("Falha ao suspender tenant {TenantId} por expiração de trial: {Error}", tenant.Id, suspendResult.Error.Message);
            }
        }

        foreach (var tenant in pastDueCandidates)
        {
            if (tenant.IsProtected)
            {
                _logger.LogWarning("Tentativa de suspender tenant protegido {TenantId} por inadimplência ignorada (IsProtected=true).", tenant.Id);
                var protHistory = TenantSubscriptionHistory.CreateFromStripeWebhook(
                    tenantId: tenant.Id,
                    actionType: SubscriptionActionType.Suspended,
                    justification: "Tentativa de suspensão automática (inadimplência fora da tolerância) ignorada devido a IsProtected=true.",
                    snapshotHeadCount: tenant.Capacity
                );
                _dbContext.SubscriptionHistories.Add(protHistory);
                protectedSkipped++;
                continue;
            }

            var suspendResult = tenant.Suspend(PastDueToleranceExceededJustification);
            if (suspendResult.IsSuccess)
            {
                var history = TenantSubscriptionHistory.CreateFromStripeWebhook(
                    tenantId: tenant.Id,
                    actionType: SubscriptionActionType.Suspended,
                    justification: PastDueToleranceExceededJustification,
                    snapshotHeadCount: tenant.Capacity
                );
                _dbContext.SubscriptionHistories.Add(history);
                suspendedPastDue++;
                _logger.LogInformation("Tenant {TenantId} suspenso automaticamente por inadimplência após prazo de tolerância de {Days} dias.", tenant.Id, pastDueGraceDays);
            }
            else
            {
                _logger.LogWarning("Falha ao suspender tenant {TenantId} por inadimplência: {Error}", tenant.Id, suspendResult.Error.Message);
            }
        }

        foreach (var tenant in activeExpiredCandidates)
        {
            if (tenant.IsProtected)
            {
                _logger.LogWarning("Tentativa de alterar tenant protegido {TenantId} por expiração de assinatura ignorada (IsProtected=true).", tenant.Id);
                var protHistory = TenantSubscriptionHistory.CreateFromStripeWebhook(
                    tenantId: tenant.Id,
                    actionType: SubscriptionActionType.Suspended,
                    justification: "Tentativa de suspensão/marcação de inadimplência ignorada devido a IsProtected=true.",
                    snapshotHeadCount: tenant.Capacity
                );
                _dbContext.SubscriptionHistories.Add(protHistory);
                protectedSkipped++;
                continue;
            }

            if (tenant.CurrentPeriodEndUtc!.Value <= pastDueCutoff)
            {
                var suspendResult = tenant.Suspend(ActiveExpiredToleranceExceededJustification);
                if (suspendResult.IsSuccess)
                {
                    var history = TenantSubscriptionHistory.CreateFromStripeWebhook(
                        tenantId: tenant.Id,
                        actionType: SubscriptionActionType.Suspended,
                        justification: ActiveExpiredToleranceExceededJustification,
                        snapshotHeadCount: tenant.Capacity
                    );
                    _dbContext.SubscriptionHistories.Add(history);
                    suspendedActive++;
                    _logger.LogInformation("Tenant {TenantId} suspenso automaticamente por expiração de assinatura além do prazo de tolerância.", tenant.Id);
                }
                else
                {
                    _logger.LogWarning("Falha ao suspender tenant {TenantId} por expiração de assinatura: {Error}", tenant.Id, suspendResult.Error.Message);
                }
            }
            else
            {
                var pastDueResult = tenant.MarkPastDue(ActiveExpiredMarkPastDueJustification);
                if (pastDueResult.IsSuccess)
                {
                    var history = TenantSubscriptionHistory.CreateFromStripeWebhook(
                        tenantId: tenant.Id,
                        actionType: SubscriptionActionType.PaymentFailed,
                        justification: ActiveExpiredMarkPastDueJustification,
                        snapshotHeadCount: tenant.Capacity
                    );
                    _dbContext.SubscriptionHistories.Add(history);
                    transitionedToPastDue++;
                    _logger.LogInformation("Tenant {TenantId} marcado como PastDue por expiração de assinatura dentro do prazo de tolerância.", tenant.Id);
                }
                else
                {
                    _logger.LogWarning("Falha ao marcar tenant {TenantId} como PastDue: {Error}", tenant.Id, pastDueResult.Error.Message);
                }
            }
        }

        if (suspendedTrials > 0 || suspendedPastDue > 0 || suspendedActive > 0 || transitionedToPastDue > 0 || protectedSkipped > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(new SubscriptionLifecycleExecutionResult(
            TotalEvaluated: totalEvaluated,
            SuspendedTrials: suspendedTrials,
            SuspendedPastDue: suspendedPastDue,
            ProtectedSkipped: protectedSkipped,
            SuspendedActive: suspendedActive,
            TransitionedToPastDue: transitionedToPastDue
        ));
    }
}
