using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Tenancy.Application.Features.SubscriptionPortal;

public sealed record CreatePortalSessionRequest(
    Guid TenantId,
    string? ReturnUrl = null
);

public sealed record CreatePortalSessionCommand(
    Guid TenantId,
    Guid UserId,
    string? ReturnUrl = null
) : IRequest<Result<CustomerPortalSessionResult>>;

public sealed class CreatePortalSessionCommandHandler : IRequestHandler<CreatePortalSessionCommand, Result<CustomerPortalSessionResult>>
{
    private readonly ITenancyDbContext _dbContext;
    private readonly IStripePaymentService _stripePaymentService;
    private readonly ISubscriptionUrlValidator _urlValidator;

    public CreatePortalSessionCommandHandler(
        ITenancyDbContext dbContext,
        IStripePaymentService stripePaymentService,
        ISubscriptionUrlValidator urlValidator)
    {
        _dbContext = dbContext;
        _stripePaymentService = stripePaymentService;
        _urlValidator = urlValidator;
    }

    public async Task<Result<CustomerPortalSessionResult>> Handle(CreatePortalSessionCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Id == request.TenantId, cancellationToken);

        if (tenant is null)
        {
            return Result.Failure<CustomerPortalSessionResult>(
                Error.NotFound("Tenant.NotFound", $"Organização/Fazenda com ID '{request.TenantId}' não foi encontrada."));
        }

        var user = await _dbContext.Users
            .Include(u => u.UserTenants)
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure<CustomerPortalSessionResult>(
                Error.NotFound("User.NotFound", $"Usuário com ID '{request.UserId}' não foi encontrado."));
        }

        var userTenant = user.UserTenants.FirstOrDefault(ut => ut.TenantId == request.TenantId);
        if (userTenant is null)
        {
            return Result.Failure<CustomerPortalSessionResult>(
                Error.Unauthorized("Auth.UnauthorizedTenant", "Usuário não pertence a esta organização/fazenda."));
        }

        if (userTenant.Role != UserRole.Admin)
        {
            return Result.Failure<CustomerPortalSessionResult>(
                Error.Unauthorized("Auth.ForbiddenBilling", "Apenas administradores da fazenda podem gerenciar planos e pagamentos."));
        }

        // Validação estrita e sanitização contra ataques de Open Redirect (CWE-601)
        var safeReturnUrlResult = _urlValidator.ResolveSafeUrl(
            request.ReturnUrl,
            $"{_urlValidator.DefaultOrigin}/settings/subscription");
        if (safeReturnUrlResult.IsFailure)
        {
            return Result.Failure<CustomerPortalSessionResult>(safeReturnUrlResult.Error);
        }

        if (string.IsNullOrWhiteSpace(tenant.StripeCustomerId))
        {
            // Se ainda não tem StripeCustomerId, cria o Customer no Stripe
            await _stripePaymentService.GetOrCreateCustomerAsync(tenant, user, cancellationToken);
        }

        var portalResult = await _stripePaymentService.CreateCustomerPortalSessionAsync(
            tenant,
            safeReturnUrlResult.Value,
            cancellationToken);

        return Result.Success(portalResult);
    }
}
