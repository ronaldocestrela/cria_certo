using CriaCerto.BuildingBlocks.Abstractions.Licensing;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Domain.Errors;
using CriaCerto.Modules.Tenancy.Application.Features.GetSubscriptionPlans;
using CriaCerto.Modules.Tenancy.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Tenancy.Application.Features.SubscriptionCheckout;

public sealed record CreateCheckoutSessionRequest(
    Guid TenantId,
    string HeadTierCode = "",
    List<string>? SelectedModules = null,
    string BillingCycle = "monthly",
    string? SuccessUrl = null,
    string? CancelUrl = null,
    string? PlanId = null
);

public sealed record CreateCheckoutSessionCommand : IRequest<Result<CheckoutSessionResult>>
{
    public Guid TenantId { get; init; }
    public Guid UserId { get; init; }
    public string HeadTierCode { get; init; } = string.Empty;
    public IReadOnlyList<string>? SelectedModules { get; init; }
    public string BillingCycle { get; init; } = "monthly";
    public string? SuccessUrl { get; init; }
    public string? CancelUrl { get; init; }
    public string? PlanId { get; init; }

    public CreateCheckoutSessionCommand() { }

    public CreateCheckoutSessionCommand(
        Guid TenantId,
        Guid UserId,
        string HeadTierCode,
        IReadOnlyList<string>? SelectedModules,
        string BillingCycle = "monthly",
        string? SuccessUrl = null,
        string? CancelUrl = null,
        string? PlanId = null)
    {
        this.TenantId = TenantId;
        this.UserId = UserId;
        this.HeadTierCode = HeadTierCode;
        this.SelectedModules = SelectedModules;
        this.BillingCycle = BillingCycle;
        this.SuccessUrl = SuccessUrl;
        this.CancelUrl = CancelUrl;
        this.PlanId = PlanId ?? HeadTierCode;
    }

    public CreateCheckoutSessionCommand(
        Guid TenantId,
        Guid UserId,
        string PlanId,
        string BillingCycle = "monthly",
        string? SuccessUrl = null,
        string? CancelUrl = null)
    {
        this.TenantId = TenantId;
        this.UserId = UserId;
        this.HeadTierCode = PlanId;
        this.PlanId = PlanId;
        this.BillingCycle = BillingCycle;
        this.SuccessUrl = SuccessUrl;
        this.CancelUrl = CancelUrl;
    }
}

public sealed class CreateCheckoutSessionCommandHandler : IRequestHandler<CreateCheckoutSessionCommand, Result<CheckoutSessionResult>>
{
    private readonly ITenancyDbContext _dbContext;
    private readonly IStripePaymentService _stripePaymentService;
    private readonly ISender _sender;
    private readonly ISubscriptionUrlValidator _urlValidator;
    private readonly ITenantActiveHerdCounter _activeHerdCounter;

    public CreateCheckoutSessionCommandHandler(
        ITenancyDbContext dbContext,
        IStripePaymentService stripePaymentService,
        ISender sender,
        ISubscriptionUrlValidator urlValidator,
        ITenantActiveHerdCounter? activeHerdCounter = null)
    {
        _dbContext = dbContext;
        _stripePaymentService = stripePaymentService;
        _sender = sender;
        _urlValidator = urlValidator;
        _activeHerdCounter = activeHerdCounter ?? new DefaultTenantActiveHerdCounter(dbContext);
    }

    public async Task<Result<CheckoutSessionResult>> Handle(CreateCheckoutSessionCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Id == request.TenantId, cancellationToken);

        if (tenant is null)
        {
            return Result.Failure<CheckoutSessionResult>(
                Error.NotFound("Tenant.NotFound", $"Organização/Fazenda com ID '{request.TenantId}' não foi encontrada."));
        }

        var user = await _dbContext.Users
            .Include(u => u.UserTenants)
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure<CheckoutSessionResult>(
                Error.NotFound("User.NotFound", $"Usuário com ID '{request.UserId}' não foi encontrado."));
        }

        var userTenant = user.UserTenants.FirstOrDefault(ut => ut.TenantId == request.TenantId);
        if (userTenant is null)
        {
            return Result.Failure<CheckoutSessionResult>(
                Error.Unauthorized("Auth.UnauthorizedTenant", "Usuário não pertence a esta organização/fazenda."));
        }

        if (userTenant.Role != UserRole.Admin)
        {
            return Result.Failure<CheckoutSessionResult>(
                Error.Unauthorized("Auth.ForbiddenBilling", "Apenas administradores da fazenda podem gerenciar planos e pagamentos."));
        }

        // Prevenção de assinaturas concorrentes / cobrança dupla no Stripe
        if (tenant.HasActiveStripeSubscription())
        {
            return Result.Failure<CheckoutSessionResult>(TenancyErrors.ActiveSubscriptionExists);
        }

        // 1. Resolução da Faixa de Cabeças (HeadTierCode) e Tradução de Planos Legados
        string rawTierOrPlan = !string.IsNullOrWhiteSpace(request.HeadTierCode)
            ? request.HeadTierCode
            : request.PlanId ?? string.Empty;

        string headTierCode;
        string? canonicalPlanId = null;
        string? planName = null;
        IReadOnlyList<string>? defaultModules = null;

        if (HeadTierCapacityLimits.IsValidHeadTierCode(rawTierOrPlan))
        {
            headTierCode = HeadTierCapacityLimits.NormalizeHeadTierCode(rawTierOrPlan);
            canonicalPlanId = headTierCode;
            planName = headTierCode;
        }
        else
        {
            var canonicalPlan = ModuleLicenseChecker.NormalizePlan(rawTierOrPlan);
            canonicalPlanId = canonicalPlan;
            switch (canonicalPlan.ToUpperInvariant())
            {
                case "STARTER":
                    headTierCode = HeadTierCapacityLimits.Tier500;
                    planName = "Plano Starter";
                    defaultModules = ["Cows", "Breeding", "Calving"];
                    break;
                case "PRO":
                    headTierCode = HeadTierCapacityLimits.Tier3000;
                    planName = "Plano Pro";
                    defaultModules = ["Cows", "Breeding", "Calving", "Growth", "Nutrition", "Sanitary"];
                    break;
                case "ENTERPRISE":
                    headTierCode = HeadTierCapacityLimits.Tier5000Plus;
                    planName = "Plano Enterprise";
                    defaultModules = ["Cows", "Breeding", "Calving", "Growth", "Nutrition", "Sanitary", "Analytics"];
                    break;
                default:
                    if (HeadTierCapacityLimits.IsValidHeadTierCode(canonicalPlan))
                    {
                        headTierCode = HeadTierCapacityLimits.NormalizeHeadTierCode(canonicalPlan);
                        canonicalPlanId = headTierCode;
                        planName = headTierCode;
                    }
                    else
                    {
                        return Result.Failure<CheckoutSessionResult>(TenancyErrors.InvalidHeadTierCode);
                    }
                    break;
            }
        }

        // 2. Normalização dos Módulos Selecionados e Garantia de Inclusão Compulsória de "Cows"
        var normalizedModules = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            HeadTierCapacityLimits.CoreModuleKey // Módulo "Cows" (Plantel) é SEMPRE obrigatório
        };

        var requestedModules = request.SelectedModules is { Count: > 0 }
            ? request.SelectedModules
            : (defaultModules ?? [HeadTierCapacityLimits.CoreModuleKey]);

        foreach (var rawModule in requestedModules)
        {
            if (string.IsNullOrWhiteSpace(rawModule))
            {
                continue;
            }

            var canonicalModule = HeadTierCapacityLimits.NormalizeCommercialModuleKey(rawModule);
            if (canonicalModule is null)
            {
                return Result.Failure<CheckoutSessionResult>(TenancyErrors.InvalidModuleKey);
            }

            normalizedModules.Add(canonicalModule);
        }

        // 3. Validação de Capacidade Ativa do Rebanho (currentHeadCount <= MaxHeads da Faixa)
        int maxHeads = HeadTierCapacityLimits.GetMaxHeadsForTier(headTierCode);
        int currentHeadCount = await _activeHerdCounter.GetActiveHeadCountAsync(tenant.Id, cancellationToken);

        if (currentHeadCount > maxHeads)
        {
            return Result.Failure<CheckoutSessionResult>(
                Error.Validation(
                    "License.HeadCapacityExceeded",
                    $"A quantidade atual de {currentHeadCount} cabeças ativas excede a capacidade máxima de {maxHeads} cabeças da faixa {headTierCode}. Selecione uma faixa de cabeças compatível com o rebanho da fazenda."));
        }

        // 4. Consulta do Catálogo Modular para Obtenção de Preços e Stripe Price IDs
        var catalogResult = await _sender.Send(new GetModularSubscriptionCatalogQuery(), cancellationToken);
        var catalog = catalogResult.IsSuccess ? catalogResult.Value : GetSubscriptionPlansQueryHandler.GetDefaultModularCatalog();

        bool isAnnual = string.Equals(request.BillingCycle, "annual", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(request.BillingCycle, "anual", StringComparison.OrdinalIgnoreCase);

        string interval = isAnnual ? "year" : "month";
        string billingCycleCanonical = isAnnual ? "annual" : "monthly";

        var lineItems = new List<StripeModularLineItem>();
        var orderedModules = PlanCapacityLimits.AllCommercialModules
            .Where(normalizedModules.Contains)
            .ToList();

        foreach (var moduleKey in orderedModules)
        {
            var moduleOption = catalog.Modules.FirstOrDefault(m => string.Equals(m.ModuleKey, moduleKey, StringComparison.OrdinalIgnoreCase));
            var priceOption = moduleOption?.PricesByTier.FirstOrDefault(p => string.Equals(p.TierCode, headTierCode, StringComparison.OrdinalIgnoreCase));

            string moduleName = moduleOption?.Name ?? moduleKey;
            decimal unitAmount = 0m;
            string? priceId = null;

            if (priceOption is not null)
            {
                unitAmount = isAnnual ? priceOption.AnnualPriceMonthly * 12 : priceOption.MonthlyPrice;
                priceId = isAnnual ? priceOption.StripePriceIdAnnual : priceOption.StripePriceIdMonthly;
            }

            lineItems.Add(new StripeModularLineItem(
                ModuleKey: moduleKey,
                ModuleName: moduleName,
                UnitAmount: unitAmount,
                Interval: interval,
                PriceId: priceId
            ));
        }

        // 5. Validação Estrita e Sanitização contra Ataques de Open Redirect (CWE-601)
        var safeSuccessUrlResult = _urlValidator.ResolveSafeUrl(
            request.SuccessUrl,
            $"{_urlValidator.DefaultOrigin}/settings/subscription?success=true");
        if (safeSuccessUrlResult.IsFailure)
        {
            return Result.Failure<CheckoutSessionResult>(safeSuccessUrlResult.Error);
        }

        var safeCancelUrlResult = _urlValidator.ResolveSafeUrl(
            request.CancelUrl,
            $"{_urlValidator.DefaultOrigin}/settings/subscription?canceled=true");
        if (safeCancelUrlResult.IsFailure)
        {
            return Result.Failure<CheckoutSessionResult>(safeCancelUrlResult.Error);
        }

        // 6. Criação da Sessão Multi-Item no Stripe
        var sessionResult = await _stripePaymentService.CreateModularCheckoutSessionAsync(
            tenant,
            user,
            headTierCode,
            maxHeads,
            orderedModules,
            billingCycleCanonical,
            lineItems,
            safeSuccessUrlResult.Value,
            safeCancelUrlResult.Value,
            canonicalPlanId,
            planName,
            cancellationToken);

        return Result.Success(sessionResult);
    }
}
