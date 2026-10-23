using System.Text.Json;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Abstractions;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Features.Enforcement.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CriaCerto.Modules.Referral.Application.Features.Enforcement.Services;

public sealed class ReferralMaturityEnforcementService : IReferralMaturityEnforcementService
{
    private readonly IReferralDbContext _dbContext;
    private readonly ITenantInfoLookupService _tenantLookupService;
    private readonly IReferralPaymentVerificationService? _paymentVerificationService;
    private readonly IReferralAuditSink? _auditSink;
    private readonly ILogger<ReferralMaturityEnforcementService>? _logger;

    public ReferralMaturityEnforcementService(
        IReferralDbContext dbContext,
        ITenantInfoLookupService tenantLookupService,
        IReferralPaymentVerificationService? paymentVerificationService = null,
        IReferralAuditSink? auditSink = null,
        ILogger<ReferralMaturityEnforcementService>? logger = null)
    {
        _dbContext = dbContext;
        _tenantLookupService = tenantLookupService;
        _paymentVerificationService = paymentVerificationService;
        _auditSink = auditSink;
        _logger = logger;
    }

    public async Task<Result<ReferralEnforcementSummaryDto>> ExecutePassAsync(
        DateTime? referenceDateUtc = null,
        CancellationToken cancellationToken = default)
    {
        var now = referenceDateUtc ?? DateTime.UtcNow;

        _logger?.LogInformation("Iniciando ciclo de maturação e enforcement de comissões (Referência UTC: {Now}).", now);

        // 1. Identificar comissões em dinheiro pendentes com carência vencida (MaturityDateUtc <= Now)
        var matureLedgers = await _dbContext.RewardLedger
            .Where(l => l.Status == LedgerRewardStatus.PendingMaturity
                     && l.RewardType == RewardType.CashCommission
                     && l.MaturityDateUtc.HasValue
                     && l.MaturityDateUtc.Value <= now)
            .ToListAsync(cancellationToken);

        if (matureLedgers.Count == 0)
        {
            _logger?.LogInformation("Nenhum lançamento pendente elegível para maturação no momento.");
            return Result.Success(new ReferralEnforcementSummaryDto(
                TotalEvaluated: 0,
                ReleasedToAvailable: 0,
                CancelledDueToRefund: 0,
                CancelledDueToTenantInsolvency: 0,
                AttributionsMarkedChurned: 0,
                ReferenceDateUtc: now));
        }

        // 2. Carregar atribuições correspondentes
        var attributionIds = matureLedgers.Select(l => l.ReferralAttributionId).Distinct().ToList();
        var attributions = await _dbContext.Attributions
            .Where(a => attributionIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        // 3. Consultar status dos tenants indicados de forma desacoplada
        var tenantIds = attributions.Values.Select(a => a.ReferredTenantId).Distinct().ToList();
        var tenantSummaries = await _tenantLookupService.GetTenantSummariesAsync(tenantIds, cancellationToken);

        var releasedCount = 0;
        var cancelledRefundCount = 0;
        var cancelledInsolvencyCount = 0;
        var churnedAttributionsCount = 0;

        foreach (var ledger in matureLedgers)
        {
            attributions.TryGetValue(ledger.ReferralAttributionId, out var attribution);
            TenantSummaryDto? tenantSummary = null;

            if (attribution != null)
            {
                tenantSummaries.TryGetValue(attribution.ReferredTenantId, out tenantSummary);
            }

            // A. Regra de Inadimplência / Cancelamento da Fazenda Indicada (PastDue / Cancelled / Suspended)
            if (tenantSummary != null && IsTenantInsolventOrCancelled(tenantSummary.Status))
            {
                var reason = $"Cancelamento compulsório: a fazenda indicada '{tenantSummary.Name}' encontra-se em status '{tenantSummary.Status}'.";
                var cancelResult = ledger.Cancel(reason);

                if (cancelResult.IsSuccess)
                {
                    cancelledInsolvencyCount++;

                    if (attribution != null && attribution.Status != AttributionStatus.Churned)
                    {
                        attribution.MarkChurned();
                        churnedAttributionsCount++;
                    }

                    if (_auditSink != null)
                    {
                        var auditPayload = JsonSerializer.Serialize(new
                        {
                            LedgerId = ledger.Id,
                            ledger.ParticipantId,
                            ledger.NetAmount,
                            ledger.Currency,
                            ReferredTenantId = attribution?.ReferredTenantId,
                            TenantStatus = tenantSummary.Status,
                            Reason = reason
                        });

                        await _auditSink.RecordSystemAuditAsync(
                            action: "Referral.CommissionCancelled.TenantInsolvent",
                            resource: $"RewardLedger/{ledger.Id}",
                            targetTenantId: attribution?.ReferredTenantId,
                            detailsJson: auditPayload,
                            cancellationToken: cancellationToken);
                    }
                }

                continue;
            }

            // B. Regra de Verificação Ativa de Estorno no Stripe (Opção C)
            if (_paymentVerificationService != null && !string.IsNullOrWhiteSpace(ledger.StripeInvoiceId))
            {
                var refundCheckResult = await _paymentVerificationService.IsInvoiceRefundedOrDisputedAsync(
                    ledger.StripeInvoiceId, cancellationToken);

                if (refundCheckResult.IsSuccess && refundCheckResult.Value)
                {
                    var reason = $"Estorno ou contestação de pagamento detectado no Stripe para a fatura '{ledger.StripeInvoiceId}'.";
                    var cancelResult = ledger.Cancel(reason);

                    if (cancelResult.IsSuccess)
                    {
                        cancelledRefundCount++;

                        if (_auditSink != null)
                        {
                            var auditPayload = JsonSerializer.Serialize(new
                            {
                                LedgerId = ledger.Id,
                                ledger.ParticipantId,
                                ledger.NetAmount,
                                ledger.StripeInvoiceId,
                                Reason = reason
                            });

                            await _auditSink.RecordSystemAuditAsync(
                                action: "Referral.CommissionCancelled.StripeRefund",
                                resource: $"RewardLedger/{ledger.Id}",
                                targetTenantId: attribution?.ReferredTenantId,
                                detailsJson: auditPayload,
                                cancellationToken: cancellationToken);
                        }
                    }

                    continue;
                }
            }

            // C. Maturação da Carência D+30 (PendingMaturity -> AvailableForPayout)
            var releaseResult = ledger.ReleaseForPayout();
            if (releaseResult.IsSuccess)
            {
                releasedCount++;
            }
            else
            {
                _logger?.LogWarning("Falha ao liberar comissão {LedgerId} para AvailableForPayout: {Error}",
                    ledger.Id, releaseResult.Error.Message);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var summary = new ReferralEnforcementSummaryDto(
            TotalEvaluated: matureLedgers.Count,
            ReleasedToAvailable: releasedCount,
            CancelledDueToRefund: cancelledRefundCount,
            CancelledDueToTenantInsolvency: cancelledInsolvencyCount,
            AttributionsMarkedChurned: churnedAttributionsCount,
            ReferenceDateUtc: now);

        _logger?.LogInformation(
            "Ciclo de maturação concluído: {Total} avaliados, {Released} liberados para saque, {Refunds} cancelados por estorno, {Insolvent} cancelados por inadimplência, {Churned} atribuições marcadas como churn.",
            summary.TotalEvaluated,
            summary.ReleasedToAvailable,
            summary.CancelledDueToRefund,
            summary.CancelledDueToTenantInsolvency,
            summary.AttributionsMarkedChurned);

        return Result.Success(summary);
    }

    private static bool IsTenantInsolventOrCancelled(string status)
    {
        return string.Equals(status, "PastDue", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Suspended", StringComparison.OrdinalIgnoreCase);
    }
}
