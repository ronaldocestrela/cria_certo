using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Domain.Errors;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Tenancy.Application.Features.RefreshToken;

public record RefreshTokenResult(
    string Token,
    Guid TenantId,
    string SubscribedPlan,
    string Role
);

public record RefreshTokenRequest(
    Guid? TenantId = null
);

public record RefreshTokenCommand(
    Guid UserId,
    Guid TenantId
) : IRequest<Result<RefreshTokenResult>>;

public sealed class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, Result<RefreshTokenResult>>
{
    private readonly ITenancyDbContext _dbContext;
    private readonly IJwtService _jwtService;

    public RefreshTokenCommandHandler(ITenancyDbContext dbContext, IJwtService jwtService)
    {
        _dbContext = dbContext;
        _jwtService = jwtService;
    }

    public async Task<Result<RefreshTokenResult>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .Include(u => u.UserTenants)
            .ThenInclude(ut => ut.Tenant)
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure<RefreshTokenResult>(
                Error.NotFound("User.NotFound", $"Usuário com ID '{request.UserId}' não foi encontrado."));
        }

        var userTenant = user.UserTenants.FirstOrDefault(ut => ut.TenantId == request.TenantId);
        if (userTenant is null)
        {
            return Result.Failure<RefreshTokenResult>(
                Error.Unauthorized("Auth.UnauthorizedTenant", "Usuário não pertence a esta organização/fazenda."));
        }

        var tenant = userTenant.Tenant!;
        if (!TenantLifecycle.CanProducerAccess(tenant.Status))
        {
            return Result.Failure<RefreshTokenResult>(TenancyErrors.TenantNotAccessible);
        }

        var newToken = _jwtService.GenerateToken(user, tenant, userTenant.Role);

        return Result.Success(new RefreshTokenResult(
            Token: newToken,
            TenantId: tenant.Id,
            SubscribedPlan: tenant.SubscribedPlan,
            Role: userTenant.Role.ToString()
        ));
    }
}
