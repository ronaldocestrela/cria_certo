using CriaCerto.BuildingBlocks.Abstractions.Licensing;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Contracts;
using CriaCerto.Modules.Tenancy.Application.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Tenancy.Application.Features.ChangeSubscriptionPlan;

public record ChangeSubscriptionPlanRequest(
    Guid TenantId,
    string NewPlan
);

public record ChangeSubscriptionPlanResult(
    string Token,
    TenantProfileDto Profile
);

public record ChangeSubscriptionPlanCommand(
    Guid TenantId,
    Guid UserId,
    string NewPlan
) : IRequest<Result<ChangeSubscriptionPlanResult>>;

public class ChangeSubscriptionPlanCommandHandler : IRequestHandler<ChangeSubscriptionPlanCommand, Result<ChangeSubscriptionPlanResult>>
{
    private readonly ITenancyDbContext _dbContext;
    private readonly IJwtService _jwtService;

    public ChangeSubscriptionPlanCommandHandler(ITenancyDbContext dbContext, IJwtService jwtService)
    {
        _dbContext = dbContext;
        _jwtService = jwtService;
    }

    public async Task<Result<ChangeSubscriptionPlanResult>> Handle(ChangeSubscriptionPlanCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Id == request.TenantId, cancellationToken);

        if (tenant is null)
        {
            return Result.Failure<ChangeSubscriptionPlanResult>(
                Error.NotFound("Tenant.NotFound", $"Organização/Fazenda com ID '{request.TenantId}' não foi encontrada."));
        }

        var user = await _dbContext.Users
            .Include(u => u.UserTenants)
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure<ChangeSubscriptionPlanResult>(
                Error.NotFound("User.NotFound", $"Usuário com ID '{request.UserId}' não foi encontrado."));
        }

        var userTenant = user.UserTenants.FirstOrDefault(ut => ut.TenantId == request.TenantId);
        if (userTenant is null)
        {
            return Result.Failure<ChangeSubscriptionPlanResult>(
                Error.Unauthorized("Auth.UnauthorizedTenant", "Usuário não pertence a esta organização/fazenda."));
        }

        if (userTenant.Role != UserRole.Admin)
        {
            return Result.Failure<ChangeSubscriptionPlanResult>(
                Error.Unauthorized("Auth.ForbiddenPlanChange", "Apenas administradores podem solicitar alteração de plano."));
        }

        tenant.SubscribedPlan = ModuleLicenseChecker.NormalizePlan(request.NewPlan);
        await _dbContext.SaveChangesAsync(cancellationToken);

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

        return Result.Success(new ChangeSubscriptionPlanResult(newToken, profileDto));
    }
}
