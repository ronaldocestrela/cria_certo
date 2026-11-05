using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Contracts;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Domain.Errors;
using CriaCerto.Modules.Tenancy.Application.Features.GetSubscriptionPlans;
using CriaCerto.Modules.Tenancy.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Tenancy.Application.Features.UpdateModularSubscription;

public sealed record UpdateModularSubscriptionRequest(
    Guid TenantId,
    string? HeadTierCode = null,
    List<string>? SelectedModules = null,
    string? BillingCycle = "monthly",
    bool AllowGracePeriod = false
);

public sealed record UpdateModularSubscriptionResult(
    Guid TenantId,
    string HeadTierCode,
    int HeadCapacity,
    IReadOnlyList<string> SubscribedModules,
    string Status,
    DateTime? GracePeriodEndsAtUtc,
    string? PendingHeadTierCode,
    string Token,
    TenantProfileDto Profile
);

public sealed record UpdateModularSubscriptionCommand(
    Guid TenantId,
    Guid UserId,
    string? HeadTierCode = null,
    IReadOnlyList<string>? SelectedModules = null,
    string? BillingCycle = "monthly",
    bool AllowGracePeriod = false
) : IRequest<Result<UpdateModularSubscriptionResult>>;

public sealed class UpdateModularSubscriptionCommandHandler : IRequestHandler<UpdateModularSubscriptionCommand, Result<UpdateModularSubscriptionResult>>
{
    private readonly ITenancyDbContext _dbContext;
    private readonly IStripePaymentService _stripePaymentService;
    private readonly IJwtService _jwtService;
    private readonly ISender _sender;
    private readonly ITenantActiveHerdCounter _activeHerdCounter;

    public UpdateModularSubscriptionCommandHandler(
        ITenancyDbContext dbContext,
        IStripePaymentService stripePaymentService,
        IJwtService jwtService,
        ISender sender,
        ITenantActiveHerdCounter? activeHerdCounter = null)
    {
        _dbContext = dbContext;
        _stripePaymentService = stripePaymentService;
        _jwtService = jwtService;
        _sender = sender;
        _activeHerdCounter = activeHerdCounter ?? new DefaultTenantActiveHerdCounter(dbContext);
    }

    public async Task<Result<UpdateModularSubscriptionResult>> Handle(UpdateModularSubscriptionCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _dbContext.Tenants
            .Include(t => t.SubscribedModules)
            .FirstOrDefaultAsync(t => t.Id == request.TenantId, cancellationToken);

        if (tenant is null)
        {
            return Result.Failure<UpdateModularSubscriptionResult>(
                Error.NotFound("Tenant.NotFound", $"Organização/Fazenda com ID '{request.TenantId}' não foi encontrada."));
        }

        var user = await _dbContext.Users
            .Include(u => u.UserTenants)
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure<UpdateModularSubscriptionResult>(
                Error.NotFound("User.NotFound", $"Usuário com ID '{request.UserId}' não foi encontrado."));
        }

        var userTenant = user.UserTenants.FirstOrDefault(ut => ut.TenantId == request.TenantId);
        if (userTenant is null)
        {
            return Result.Failure<UpdateModularSubscriptionResult>(
                Error.Unauthorized("Auth.UnauthorizedTenant", "Usuário não pertence a esta organização/fazenda."));
        }

        if (userTenant.Role != UserRole.Admin)
        {
            return Result.Failure<UpdateModularSubscriptionResult>(
                Error.Unauthorized("Auth.ForbiddenBilling", "Apenas administradores da fazenda podem gerenciar planos e pagamentos."));
        }

        if (string.IsNullOrWhiteSpace(tenant.StripeSubscriptionId) || !tenant.HasActiveStripeSubscription())
        {
            return Result.Failure<UpdateModularSubscriptionResult>(TenancyErrors.NoActiveSubscription);
        }

        // 1. Resolução da Faixa de Cabeças (Target HeadTierCode)
        string targetTierCode = !string.IsNullOrWhiteSpace(request.HeadTierCode)
            ? PlanCapacityLimits.NormalizeHeadTierCode(request.HeadTierCode)
            : tenant.HeadTierCode;

        if (!PlanCapacityLimits.IsValidHeadTierCode(targetTierCode))
        {
            return Result.Failure<UpdateModularSubscriptionResult>(TenancyErrors.InvalidHeadTierCode);
        }

        int targetMaxHeads = PlanCapacityLimits.GetMaxHeadsForTier(targetTierCode);

        // 2. Normalização dos Módulos Selecionados (Módulo "Cows" é estritamente obrigatório)
        var normalizedModules = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            PlanCapacityLimits.CoreModuleKey
        };

        var requestedModules = request.SelectedModules is { Count: > 0 }
            ? request.SelectedModules
            : tenant.GetSubscribedModuleKeys();

        foreach (var rawModule in requestedModules)
        {
            if (string.IsNullOrWhiteSpace(rawModule))
            {
                continue;
            }

            var canonicalModule = PlanCapacityLimits.NormalizeCommercialModuleKey(rawModule);
            if (canonicalModule is null)
            {
                return Result.Failure<UpdateModularSubscriptionResult>(TenancyErrors.InvalidModuleKey);
            }

            normalizedModules.Add(canonicalModule);
        }

        var orderedTargetModules = PlanCapacityLimits.AllCommercialModules
            .Where(normalizedModules.Contains)
            .ToList();

        // 3. Validação de Capacidade Ativa do Rebanho e Regra de Downgrade com Grace Period
        int currentHeadCount = await _activeHerdCounter.GetActiveHeadCountAsync(tenant.Id, cancellationToken);
        bool usageExceeds = currentHeadCount > targetMaxHeads;
        bool isGracePeriodActivated = false;
        DateTime? gracePeriodEndsAtUtc = null;
        string? pendingHeadTierCode = null;

        if (usageExceeds)
        {
            if (!request.AllowGracePeriod)
            {
                return Result.Failure<UpdateModularSubscriptionResult>(
                    Error.Validation(
                        "License.HeadCapacityExceeded",
                        $"A quantidade atual de {currentHeadCount} cabeças ativas excede a capacidade máxima de {targetMaxHeads} cabeças da faixa {targetTierCode}. Reduza o rebanho ou habilite a carência (Grace Period) de 14 dias para prosseguir."));
            }

            isGracePeriodActivated = true;
            gracePeriodEndsAtUtc = DateTime.UtcNow.AddDays(14);
            pendingHeadTierCode = targetTierCode;
        }

        // 4. Consulta do Catálogo Modular para Obtenção dos Preços Atualizados e Stripe Price IDs
        var catalogResult = await _sender.Send(new GetModularSubscriptionCatalogQuery(), cancellationToken);
        var catalog = catalogResult.IsSuccess ? catalogResult.Value : GetSubscriptionPlansQueryHandler.GetDefaultModularCatalog();

        bool isAnnual = string.Equals(request.BillingCycle, "annual", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(request.BillingCycle, "anual", StringComparison.OrdinalIgnoreCase);

        string interval = isAnnual ? "year" : "month";
        string billingCycleCanonical = isAnnual ? "annual" : "monthly";

        // Se Grace Period ativado, a cobrança da faixa é mantida na faixa atual ou calculada para a nova?
        // Na atualização do Stripe, aplicamos os novos preços modulares da faixa alvo.
        var lineItems = new List<StripeModularLineItem>();
        var priceSnapshots = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        foreach (var moduleKey in orderedTargetModules)
        {
            var moduleOption = catalog.Modules.FirstOrDefault(m => string.Equals(m.ModuleKey, moduleKey, StringComparison.OrdinalIgnoreCase));
            var priceOption = moduleOption?.PricesByTier.FirstOrDefault(p => string.Equals(p.TierCode, targetTierCode, StringComparison.OrdinalIgnoreCase));

            string moduleName = moduleOption?.Name ?? moduleKey;
            decimal unitAmount = 0m;
            string? priceId = null;

            if (priceOption is not null)
            {
                unitAmount = isAnnual ? priceOption.AnnualPriceMonthly * 12 : priceOption.MonthlyPrice;
                priceId = isAnnual ? priceOption.StripePriceIdAnnual : priceOption.StripePriceIdMonthly;
                priceSnapshots[moduleKey] = priceOption.MonthlyPrice;
            }

            lineItems.Add(new StripeModularLineItem(
                ModuleKey: moduleKey,
                ModuleName: moduleName,
                UnitAmount: unitAmount,
                Interval: interval,
                PriceId: priceId
            ));
        }

        // 5. Atualização Multi-Item no Stripe com ProrationBehavior = "create_prorations"
        var stripeUpdateResult = await _stripePaymentService.UpdateModularSubscriptionAsync(
            tenant,
            targetTierCode,
            targetMaxHeads,
            orderedTargetModules,
            billingCycleCanonical,
            lineItems,
            cancellationToken);

        if (stripeUpdateResult.IsFailure)
        {
            return Result.Failure<UpdateModularSubscriptionResult>(stripeUpdateResult.Error);
        }

        // 6. Atualização do Estado do Tenant e Módulos Subscritos
        int currentCapacity = tenant.Capacity;
        string previousTierCode = tenant.HeadTierCode;

        if (!isGracePeriodActivated)
        {
            tenant.SetHeadTier(targetTierCode);
            tenant.Status = "Active";
            tenant.StatusReason = null;
        }
        else
        {
            tenant.StatusReason = $"Grace Period ativo de 14 dias até {gracePeriodEndsAtUtc:yyyy-MM-dd HH:mm:ss} UTC para adequação ao limite de {targetMaxHeads} cabeças da faixa {targetTierCode} (rebanho ativo atual: {currentHeadCount}).";
        }

        var stripeItemIdsNullable = stripeUpdateResult.Value.SubscriptionItemIdsByModule
            .ToDictionary(kvp => kvp.Key, kvp => (string?)kvp.Value, StringComparer.OrdinalIgnoreCase);

        var existingModuleKeys = tenant.SubscribedModules
            .Select(m => m.ModuleKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var modulesToRemove = tenant.SubscribedModules
            .Where(m => !normalizedModules.Contains(m.ModuleKey))
            .ToList();

        if (modulesToRemove.Count > 0)
        {
            _dbContext.TenantSubscribedModules.RemoveRange(modulesToRemove);
        }

        tenant.SetSubscribedModules(
            orderedTargetModules,
            priceSnapshots,
            stripeItemIdsNullable,
            DateTime.UtcNow);

        foreach (var mod in tenant.SubscribedModules)
        {
            if (!existingModuleKeys.Contains(mod.ModuleKey))
            {
                _dbContext.TenantSubscribedModules.Add(mod);
            }
        }

        // 7. Registro de Histórico de Assinatura
        SubscriptionActionType actionType;
        string historyJustification;

        if (isGracePeriodActivated)
        {
            actionType = SubscriptionActionType.DowngradeGracePeriodStarted;
            historyJustification = $"Downgrade para {targetTierCode} com rebanho ativo de {currentHeadCount} cabeças excedente. Grace Period ativado até {gracePeriodEndsAtUtc:yyyy-MM-dd HH:mm:ss} UTC.";
        }
        else if (targetMaxHeads > currentCapacity)
        {
            actionType = SubscriptionActionType.Upgrade;
            historyJustification = $"Upgrade de faixa de {previousTierCode} para {targetTierCode} e sincronização de módulos ({string.Join(", ", orderedTargetModules)}).";
        }
        else if (targetMaxHeads < currentCapacity)
        {
            actionType = SubscriptionActionType.DowngradeImmediate;
            historyJustification = $"Downgrade imediato de faixa de {previousTierCode} para {targetTierCode} (rebanho ativo: {currentHeadCount} cabeças).";
        }
        else
        {
            actionType = SubscriptionActionType.PlanChanged;
            historyJustification = $"Atualização modular de assinatura na faixa {targetTierCode}: [{string.Join(", ", orderedTargetModules)}].";
        }

        var history = TenantSubscriptionHistory.Create(
            tenantId: tenant.Id,
            previousPlanVersionId: null,
            newPlanVersionId: Guid.Empty,
            changedByAdminUserId: user.Id,
            justification: historyJustification,
            actionType: actionType,
            snapshotHeadCount: currentHeadCount,
            snapshotUserCount: 1,
            snapshotUnitCount: tenant.ProductionUnits?.Count ?? 0
        );
        _dbContext.SubscriptionHistories.Add(history);

        tenant.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        // 8. Emissão de Novo JWT com as Claims Modulares Sincronizadas
        var newToken = _jwtService.GenerateToken(user, tenant, userTenant.Role);

        var profileDto = new TenantProfileDto(
            tenant.Id,
            tenant.Name,
            tenant.CNPJ,
            tenant.Status,
            tenant.SubscribedPlan,
            tenant.Capacity,
            tenant.State,
            tenant.City,
            tenant.StateRegistration,
            tenant.AreaInHectares,
            tenant.Type
        );

        return Result.Success(new UpdateModularSubscriptionResult(
            TenantId: tenant.Id,
            HeadTierCode: tenant.HeadTierCode,
            HeadCapacity: tenant.Capacity,
            SubscribedModules: tenant.GetSubscribedModuleKeys(),
            Status: isGracePeriodActivated ? "GracePeriodActive" : tenant.Status,
            GracePeriodEndsAtUtc: gracePeriodEndsAtUtc,
            PendingHeadTierCode: pendingHeadTierCode,
            Token: newToken,
            Profile: profileDto
        ));
    }
}
