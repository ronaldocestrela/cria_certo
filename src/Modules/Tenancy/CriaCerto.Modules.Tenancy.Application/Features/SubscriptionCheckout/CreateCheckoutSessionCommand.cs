using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
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

    public CreateCheckoutSessionCommandHandler(
        ITenancyDbContext dbContext,
        IStripePaymentService stripePaymentService,
        ISender sender)
    {
        _dbContext = dbContext;
        _stripePaymentService = stripePaymentService;
        _sender = sender;
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

        // Consultar catálogo de planos para obter valores e identificadores de preço
        var plansResult = await _sender.Send(new GetSubscriptionPlansQuery(), cancellationToken);
        var plans = plansResult.IsSuccess ? plansResult.Value : new List<SubscriptionPlanDto>();

        var selectedPlan = plans.FirstOrDefault(p =>
            string.Equals(p.PlanId, request.PlanId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.Name, request.PlanId, StringComparison.OrdinalIgnoreCase));

        bool isAnnual = string.Equals(request.BillingCycle, "annual", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(request.BillingCycle, "anual", StringComparison.OrdinalIgnoreCase);

        decimal amount = selectedPlan != null
            ? (isAnnual ? selectedPlan.AnnualPriceMonthly * 12 : selectedPlan.MonthlyPrice)
            : (isAnnual ? 119m * 12 : 149m);

        string? stripePriceId = isAnnual
            ? selectedPlan?.StripePriceIdAnnual
            : selectedPlan?.StripePriceIdMonthly;

        string planName = selectedPlan?.Name ?? request.PlanId;
        string cycle = isAnnual ? "year" : "month";

        var sessionResult = await _stripePaymentService.CreateCheckoutSessionAsync(
            tenant,
            user,
            planName,
            cycle,
            amount,
            request.SuccessUrl ?? string.Empty,
            request.CancelUrl ?? string.Empty,
            stripePriceId,
            cancellationToken);

        return Result.Success(sessionResult);
    }
}
