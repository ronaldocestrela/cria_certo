using System.Text.Json;
using CriaCerto.Modules.Referral.Application.Abstractions;
using CriaCerto.Modules.Referral.Application.Domain;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Tenancy.Application.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CriaCerto.Modules.Referral.Application.Features.EventHandlers;

public sealed class TenantPaymentSucceededReferralHandler : INotificationHandler<TenantPaymentSucceededIntegrationEvent>
{
    private readonly IReferralDbContext _dbContext;
    private readonly IReferralPaymentVerificationService? _paymentVerificationService;
    private readonly ITenantInfoLookupService? _tenantLookupService;
    private readonly IReferralAuditSink? _auditSink;
    private readonly ILogger<TenantPaymentSucceededReferralHandler>? _logger;

    public TenantPaymentSucceededReferralHandler(
        IReferralDbContext dbContext,
        IReferralPaymentVerificationService? paymentVerificationService = null,
        ITenantInfoLookupService? tenantLookupService = null,
        IReferralAuditSink? auditSink = null,
        ILogger<TenantPaymentSucceededReferralHandler>? logger = null)
    {
        _dbContext = dbContext;
        _paymentVerificationService = paymentVerificationService;
        _tenantLookupService = tenantLookupService;
        _auditSink = auditSink;
        _logger = logger;
    }

    public async Task Handle(TenantPaymentSucceededIntegrationEvent notification, CancellationToken cancellationToken)
    {
        var attribution = await _dbContext.Attributions
            .FirstOrDefaultAsync(a => a.ReferredTenantId == notification.TenantId, cancellationToken);

        if (attribution == null)
        {
            return;
        }

        if (attribution.Status != AttributionStatus.Pending)
        {
            _logger?.LogInformation("Atribuição {AttributionId} para Tenant {TenantId} já se encontra em status '{Status}'. Ignorando conversão.",
                attribution.Id, notification.TenantId, attribution.Status);
            return;
        }

        var participant = await _dbContext.Participants
            .FirstOrDefaultAsync(p => p.Id == attribution.ReferrerParticipantId, cancellationToken);

        if (participant == null)
        {
            _logger?.LogWarning("Participante indicador {ParticipantId} não encontrado para atribuição {AttributionId}.",
                attribution.ReferrerParticipantId, attribution.Id);
            return;
        }

        if (participant.Status == ParticipantStatus.Suspended)
        {
            _logger?.LogWarning("Participante indicador {ParticipantId} está suspenso. Abortando conversão da atribuição {AttributionId}.",
                participant.Id, attribution.Id);
            return;
        }

        // 1. Verificação de Colisão de Cartão de Crédito (Anti-Fraude Stripe)
        if (_paymentVerificationService != null && participant.ParticipantType == ParticipantType.Customer && participant.TenantId.HasValue)
        {
            var referrerTenant = _tenantLookupService != null
                ? await _tenantLookupService.GetTenantSummaryAsync(participant.TenantId.Value, cancellationToken)
                : null;

            if (referrerTenant != null && !string.IsNullOrWhiteSpace(referrerTenant.StripeCustomerId))
            {
                var cardCheckResult = await _paymentVerificationService.HasCardFingerprintCollisionAsync(
                    notification.StripeCustomerId,
                    referrerTenant.StripeCustomerId,
                    notification.StripeCardFingerprint,
                    cancellationToken);

                if (cardCheckResult.IsSuccess && cardCheckResult.Value)
                {
                    _logger?.LogWarning("Fraude de cartão de crédito detectada: Pagante {PayingCustomer} e Indicador {ReferrerCustomer} utilizam o mesmo cartão.",
                        notification.StripeCustomerId, referrerTenant.StripeCustomerId);

                    attribution.FlagAsFraud("Auto-indicação detectada: mesmo cartão de crédito registrado no Stripe entre pagante e indicador.");

                    var suspendReason = "Suspensão preventiva automática: tentativa de auto-indicação com mesmo cartão de crédito Stripe.";
                    participant.Suspend(suspendReason);

                    await _dbContext.SaveChangesAsync(cancellationToken);

                    if (_auditSink != null)
                    {
                        var auditDetails = JsonSerializer.Serialize(new
                        {
                            Action = "CardFingerprintCollisionDetected",
                            ReferredTenantId = notification.TenantId,
                            ReferrerParticipantId = participant.Id,
                            ReferrerTenantId = participant.TenantId,
                            PayingStripeCustomerId = notification.StripeCustomerId,
                            ReferrerStripeCustomerId = referrerTenant.StripeCustomerId,
                            StripeInvoiceId = notification.StripeInvoiceId,
                            ParticipantStatus = "Suspended",
                            AttributionStatus = "Fraudulent"
                        });

                        await _auditSink.RecordSystemAuditAsync(
                            action: "Referral.AntiFraud.CardFingerprintCollision",
                            resource: $"ReferralAttribution/{attribution.Id}",
                            targetTenantId: notification.TenantId,
                            detailsJson: auditDetails,
                            cancellationToken: cancellationToken);
                    }

                    Referral.Application.Telemetry.ReferralTelemetry.RecordAttribution(participant.ParticipantType.ToString(), "Fraudulent");
                    return;
                }
            }
        }

        // 2. Transição para Convertido
        var conversionResult = attribution.MarkConverted(notification.PaymentDateUtc);
        if (conversionResult.IsFailure)
        {
            _logger?.LogError("Erro ao converter atribuição {AttributionId}: {Error}",
                attribution.Id, conversionResult.Error.Message);
            return;
        }

        Referral.Application.Telemetry.ReferralTelemetry.RecordAttribution(participant.ParticipantType.ToString(), "Converted");

        // 3. Emissão da Recompensa correspondente
        if (participant.ParticipantType == ParticipantType.CommercialPartner)
        {
            var contract = await _dbContext.PartnerContracts
                .Where(c => c.ParticipantId == participant.Id && c.IsActive)
                .OrderByDescending(c => c.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            if (contract != null)
            {
                decimal commissionAmount = contract.CommissionType switch
                {
                    CommissionType.FixedPerConversion => contract.CommissionValue,
                    CommissionType.PercentageFirstMonth or CommissionType.PercentageRecurring =>
                        notification.AmountPaid * (contract.CommissionValue / 100m),
                    _ => 0m
                };

                if (commissionAmount > 0)
                {
                    var maturityDate = DateTime.UtcNow.AddDays(contract.PaymentTermsDays <= 0 ? 30 : contract.PaymentTermsDays);
                    var ledgerResult = ReferralRewardLedger.CreateCashCommission(
                        referralAttributionId: attribution.Id,
                        participantId: participant.Id,
                        grossAmount: commissionAmount,
                        netAmount: commissionAmount,
                        currency: notification.Currency,
                        maturityDateUtc: maturityDate,
                        stripeInvoiceId: notification.StripeInvoiceId);

                    if (ledgerResult.IsSuccess)
                    {
                        _dbContext.RewardLedger.Add(ledgerResult.Value);
                        Referral.Application.Telemetry.ReferralTelemetry.RecordReward("CashCommission", (double)commissionAmount, notification.Currency);
                        _logger?.LogInformation("Comissão de R$ {Amount} gerada em PendingMaturity para Parceiro {ParticipantId} (Atribuição {AttributionId}).",
                            commissionAmount, participant.Id, attribution.Id);
                    }
                }
            }
        }
        else if (participant.ParticipantType == ParticipantType.Customer && participant.TenantId.HasValue)
        {
            var referrerTenant = _tenantLookupService != null
                ? await _tenantLookupService.GetTenantSummaryAsync(participant.TenantId.Value, cancellationToken)
                : null;

            if (referrerTenant != null && !string.IsNullOrWhiteSpace(referrerTenant.PlanCode))
            {
                var policy = await _dbContext.PlanDiscountPolicies
                    .FirstOrDefaultAsync(p => p.PlanCode.ToUpper() == referrerTenant.PlanCode.ToUpper() && p.IsActive, cancellationToken);

                if (policy != null)
                {
                    var basePrice = referrerTenant.PlanBasePrice ?? notification.AmountPaid;
                    var discountAmount = policy.CalculateDiscount(basePrice);

                    if (discountAmount > 0)
                    {
                        var creditResult = ReferralRewardLedger.CreateInvoiceDiscountCredit(
                            referralAttributionId: attribution.Id,
                            participantId: participant.Id,
                            discountAmount: discountAmount,
                            currency: notification.Currency,
                            stripeInvoiceId: notification.StripeInvoiceId);

                        if (creditResult.IsSuccess)
                        {
                            _dbContext.RewardLedger.Add(creditResult.Value);
                            Referral.Application.Telemetry.ReferralTelemetry.RecordReward("InvoiceDiscountCredit", (double)creditResult.Value.GrossAmount, notification.Currency);
                            _logger?.LogInformation("Crédito de desconto de R$ {Amount} gerado para Produtor {ParticipantId} (Atribuição {AttributionId}).",
                                discountAmount, participant.Id, attribution.Id);
                        }
                    }
                }
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
