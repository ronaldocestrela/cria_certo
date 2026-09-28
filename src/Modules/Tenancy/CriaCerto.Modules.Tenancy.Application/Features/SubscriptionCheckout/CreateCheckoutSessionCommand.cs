using CriaCerto.BuildingBlocks.Abstractions.Licensing;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Domain.Errors;
using CriaCerto.Modules.Tenancy.Application.Features.GetSubscriptionPlans;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Tenancy.Application.Features.SubscriptionCheckout;

public sealed record CreateCheckoutSessionRequest(
    Guid TenantId,
    string PlanId,
    string BillingCycle = "monthly",
    string? SuccessUrl = null,
    string? CancelUrl = null
);

public sealed record CreateCheckoutSessionCommand(
    Guid TenantId,
    Guid UserId,
    string PlanId,
    string BillingCycle = "monthly",
    string? SuccessUrl = null,
    string? CancelUrl = null
) : IRequest<Result<CheckoutSessionResult>>;

public sealed class CreateCheckoutSessionCommandHandler : IRequestHandler<CreateCheckoutSessionCommand, Result<CheckoutSessionResult>>
{
    private readonly ITenancyDbContext _dbContext;
    private readonly IStripePaymentService _stripePaymentService;
    private readonly ISender _sender;
    private readonly ISubscriptionUrlValidator _urlValidator;

    public CreateCheckoutSessionCommandHandler(
        ITenancyDbContext dbContext,
        IStripePaymentService stripePaymentService,
        ISender sender,
        ISubscriptionUrlValidator urlValidator)
    {
        _dbContext = dbContext;
        _stripePaymentService = stripePaymentService;
        _sender = sender;
        _urlValidator = urlValidator;
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

        // Consultar catálogo de planos para obter valores e identificadores de preço
        var plansResult = await _sender.Send(new GetSubscriptionPlansQuery(), cancellationToken);
        var plans = plansResult.IsSuccess ? plansResult.Value : new List<SubscriptionPlanDto>();

        var canonicalPlan = ModuleLicenseChecker.NormalizePlan(request.PlanId);

        var selectedPlan = plans.FirstOrDefault(p =>
            string.Equals(p.PlanId, request.PlanId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.Name, request.PlanId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.PlanId, canonicalPlan, StringComparison.OrdinalIgnoreCase));

        string canonicalPlanId = selectedPlan?.PlanId ?? canonicalPlan;
        string planName = selectedPlan?.Name ?? canonicalPlanId;

        bool isAnnual = string.Equals(request.BillingCycle, "annual", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(request.BillingCycle, "anual", StringComparison.OrdinalIgnoreCase);

        decimal amount = selectedPlan != null
            ? (isAnnual ? selectedPlan.AnnualPriceMonthly * 12 : selectedPlan.MonthlyPrice)
            : (isAnnual ? 119m * 12 : 149m);

        string? stripePriceId = isAnnual
            ? selectedPlan?.StripePriceIdAnnual
            : selectedPlan?.StripePriceIdMonthly;

        string cycle = isAnnual ? "year" : "month";

        // Validação estrita e sanitização contra ataques de Open Redirect (CWE-601)
        var safeSuccessUrlResult = _urlValidator.ResolveSafeUrl(
            request.SuccessUrl,
            "http://localhost:8081/settings/subscription?success=true");
        if (safeSuccessUrlResult.IsFailure)
        {
            return Result.Failure<CheckoutSessionResult>(safeSuccessUrlResult.Error);
        }

        var safeCancelUrlResult = _urlValidator.ResolveSafeUrl(
            request.CancelUrl,
            "http://localhost:8081/settings/subscription?canceled=true");
        if (safeCancelUrlResult.IsFailure)
        {
            return Result.Failure<CheckoutSessionResult>(safeCancelUrlResult.Error);
        }

        var sessionResult = await _stripePaymentService.CreateCheckoutSessionAsync(
            tenant,
            user,
            canonicalPlanId,
            planName,
            cycle,
            amount,
            safeSuccessUrlResult.Value,
            safeCancelUrlResult.Value,
            stripePriceId,
            cancellationToken);

        return Result.Success(sessionResult);
    }
}
