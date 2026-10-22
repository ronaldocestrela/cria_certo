using System.Globalization;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Abstractions;
using CriaCerto.Modules.Referral.Application.Domain;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Domain.Errors;
using CriaCerto.Modules.Referral.Application.Domain.Services;
using CriaCerto.Modules.Referral.Application.Features.CustomerReferral.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Referral.Application.Features.CustomerReferral.Queries;

public sealed record GetCustomerReferralDashboardQuery(Guid TenantId) : IRequest<Result<CustomerReferralDashboardDto>>;

public sealed class GetCustomerReferralDashboardQueryHandler : IRequestHandler<GetCustomerReferralDashboardQuery, Result<CustomerReferralDashboardDto>>
{
    private const string BaseReferralUrl = "https://criacerto.com.br/r/";
    private static readonly CultureInfo PtBrCulture = CultureInfo.GetCultureInfo("pt-BR");
    private readonly IReferralDbContext _dbContext;
    private readonly ITenantInfoLookupService _tenantLookupService;

    public GetCustomerReferralDashboardQueryHandler(
        IReferralDbContext dbContext,
        ITenantInfoLookupService tenantLookupService)
    {
        _dbContext = dbContext;
        _tenantLookupService = tenantLookupService;
    }

    public async Task<Result<CustomerReferralDashboardDto>> Handle(
        GetCustomerReferralDashboardQuery request,
        CancellationToken cancellationToken)
    {
        if (request.TenantId == Guid.Empty)
        {
            return Result.Failure<CustomerReferralDashboardDto>(ReferralErrors.Participant.TenantRequiredForCustomer);
        }

        // 1. Obter ou criar o participante Customer sob demanda
        var participant = await _dbContext.Participants
            .FirstOrDefaultAsync(p => p.TenantId == request.TenantId, cancellationToken);

        if (participant == null)
        {
            var createResult = ReferralProgramParticipant.CreateCustomer(request.TenantId);
            if (createResult.IsFailure)
            {
                return Result.Failure<CustomerReferralDashboardDto>(createResult.Error);
            }

            participant = createResult.Value;
            _dbContext.Participants.Add(participant);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        // 2. Obter ou criar o código de indicação do produtor
        var referralCode = await _dbContext.ReferralCodes
            .FirstOrDefaultAsync(c => c.ParticipantId == participant.Id && c.IsActive, cancellationToken);

        if (referralCode == null)
        {
            var tenantSummary = await _tenantLookupService.GetTenantSummaryAsync(request.TenantId, cancellationToken);
            var farmName = tenantSummary?.Name ?? tenantSummary?.TradeName ?? "FAZENDA";
            var baseSlug = ReferralCodeNormalizer.NormalizeSlug(farmName);
            if (string.IsNullOrWhiteSpace(baseSlug) || baseSlug.Length < 3)
            {
                baseSlug = "FAZENDA";
            }

            var uniqueCode = await ResolveUniqueReferralCodeAsync(baseSlug, cancellationToken);
            var codeResult = ReferralCode.Create(participant.Id, uniqueCode, "Link Padrão da Fazenda");
            if (codeResult.IsFailure)
            {
                return Result.Failure<CustomerReferralDashboardDto>(codeResult.Error);
            }

            referralCode = codeResult.Value;
            _dbContext.ReferralCodes.Add(referralCode);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        // 3. Buscar atribuições da fazenda
        var attributions = await _dbContext.Attributions
            .Where(a => a.ReferrerParticipantId == participant.Id)
            .OrderByDescending(a => a.AttributionDateUtc)
            .ToListAsync(cancellationToken);

        var activeReferralsCount = attributions.Count(a => a.Status == AttributionStatus.Converted);
        var pendingTrialReferralsCount = attributions.Count(a => a.Status == AttributionStatus.Pending);

        // 4. Buscar informações do plano atual do tenant e sua política de desconto
        var currentTenantInfo = await _tenantLookupService.GetTenantSummaryAsync(request.TenantId, cancellationToken);
        var planCode = currentTenantInfo?.PlanCode;
        var planName = currentTenantInfo?.PlanName ?? planCode;

        PlanReferralDiscountPolicy? policy = null;
        if (!string.IsNullOrWhiteSpace(planCode))
        {
            policy = await _dbContext.PlanDiscountPolicies
                .FirstOrDefaultAsync(p => p.PlanCode.ToUpper() == planCode.ToUpper() && p.IsActive, cancellationToken);
        }

        // 5. Calcular o desconto acumulado
        decimal accumulatedDiscount = 0;
        string formattedDiscount = "R$ 0,00";
        string discountDescription = "Nenhuma política de desconto configurada para o plano atual.";

        if (policy != null)
        {
            if (policy.DiscountType == DiscountType.FixedValue)
            {
                accumulatedDiscount = activeReferralsCount * policy.DiscountValue;
                formattedDiscount = $"R$ {accumulatedDiscount.ToString("N2", PtBrCulture)}";
                discountDescription = $"R$ {policy.DiscountValue.ToString("N2", PtBrCulture)} de desconto na fatura por fazenda ativa indicada";
            }
            else // Percentage
            {
                var percentage = Math.Min(activeReferralsCount * policy.DiscountValue, policy.MaxDiscountCapPercentage);
                accumulatedDiscount = percentage;
                formattedDiscount = $"{percentage.ToString("N0", PtBrCulture)}%";
                discountDescription = $"{policy.DiscountValue.ToString("N0", PtBrCulture)}% de desconto na fatura por fazenda ativa (máximo de {policy.MaxDiscountCapPercentage.ToString("N0", PtBrCulture)}%)";
            }
        }

        // 6. Detalhar as fazendas indicadas com mascaramento LGPD
        var referredTenantIds = attributions.Select(a => a.ReferredTenantId).Distinct().ToList();
        var referredSummaries = await _tenantLookupService.GetTenantSummariesAsync(referredTenantIds, cancellationToken);

        var referredFarmDtos = new List<ReferredFarmSummaryDto>();
        foreach (var attr in attributions)
        {
            referredSummaries.TryGetValue(attr.ReferredTenantId, out var referredSummary);
            var farmRawName = referredSummary?.TradeName ?? referredSummary?.Name ?? "Fazenda";
            var maskedName = LgpdFarmNameMasker.Mask(farmRawName);

            string statusKey;
            string statusDesc;
            string benefitDesc;

            switch (attr.Status)
            {
                case AttributionStatus.Converted:
                    statusKey = "ActiveGeneratingDiscount";
                    statusDesc = "Ativa — Desconto Aplicado";
                    benefitDesc = policy != null
                        ? (policy.DiscountType == DiscountType.FixedValue ? $"+ R$ {policy.DiscountValue.ToString("N2", PtBrCulture)}/mês" : $"+ {policy.DiscountValue.ToString("N0", PtBrCulture)}%")
                        : "Ativo";
                    break;
                case AttributionStatus.Pending:
                    statusKey = "InTrialPendingPayment";
                    statusDesc = "Avaliação Gratuita (Trial) — Desconto liberado após o 1º pagamento da assinatura";
                    benefitDesc = "Aguardando 1º Pagamento";
                    break;
                case AttributionStatus.Churned:
                    statusKey = "Cancelled";
                    statusDesc = "Assinatura Inativa / Cancelada";
                    benefitDesc = "Inativo";
                    break;
                default:
                    statusKey = "Fraudulent";
                    statusDesc = "Não Elegível";
                    benefitDesc = "Bloqueado";
                    break;
            }

            referredFarmDtos.Add(new ReferredFarmSummaryDto(
                AttributionId: attr.Id,
                MaskedFarmName: maskedName,
                AttributionDateUtc: attr.AttributionDateUtc,
                ConvertedAtUtc: attr.ConvertedAtUtc,
                Status: statusKey,
                StatusDescription: statusDesc,
                EstimatedBenefitDescription: benefitDesc));
        }

        var referralUrl = $"{BaseReferralUrl}{referralCode.Code}";
        var whatsAppShareText = $"Olá! Estou usando o Cria Certo para gerenciar minha fazenda e recomendo para você. Cadastre-se pelo meu link para conhecer a plataforma: {referralUrl}";

        var dto = new CustomerReferralDashboardDto(
            ReferralCode: referralCode.Code,
            ReferralUrl: referralUrl,
            AccumulatedDiscountNextInvoice: accumulatedDiscount,
            FormattedAccumulatedDiscount: formattedDiscount,
            ActiveReferralsCount: activeReferralsCount,
            PendingTrialReferralsCount: pendingTrialReferralsCount,
            CurrentPlanCode: planCode,
            CurrentPlanName: planName,
            DiscountPerReferralDescription: discountDescription,
            WhatsAppShareText: whatsAppShareText,
            ReferredFarms: referredFarmDtos);

        return Result.Success(dto);
    }

    private async Task<string> ResolveUniqueReferralCodeAsync(string baseSlug, CancellationToken cancellationToken)
    {
        var candidate = baseSlug;
        var suffix = 2;

        while (await _dbContext.ReferralCodes.AnyAsync(c => c.Code == candidate, cancellationToken))
        {
            candidate = $"{baseSlug}-{suffix}";
            suffix++;
        }

        return candidate;
    }
}
