using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Contracts;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Domain.Errors;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Tenancy.Application.Features.SelectTenant;

public record SelectTenantCommand(Guid UserId, Guid TenantId) : IRequest<Result<AuthResponse>>;

public sealed class SelectTenantCommandHandler : IRequestHandler<SelectTenantCommand, Result<AuthResponse>>
{
    private readonly ITenancyDbContext _dbContext;
    private readonly IJwtService _jwtService;

    public SelectTenantCommandHandler(ITenancyDbContext dbContext, IJwtService jwtService)
    {
        _dbContext = dbContext;
        _jwtService = jwtService;
    }

    public async Task<Result<AuthResponse>> Handle(SelectTenantCommand request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .Include(u => u.UserTenants)
            .ThenInclude(ut => ut.Tenant)
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (user == null)
        {
            return Result.Failure<AuthResponse>(
                Error.NotFound("User.NotFound", "Usuário não encontrado."));
        }

        var userTenant = user.UserTenants.FirstOrDefault(ut => ut.TenantId == request.TenantId);
        if (userTenant == null)
        {
            return Result.Failure<AuthResponse>(
                Error.Unauthorized("Auth.UnauthorizedTenant", "Usuário não tem acesso a esta granja."));
        }

        var tenant = userTenant.Tenant!;
        if (!TenantLifecycle.CanProducerAccess(tenant.Status))
        {
            return Result.Failure<AuthResponse>(TenancyErrors.TenantNotAccessible);
        }

        var token = _jwtService.GenerateToken(user, tenant, userTenant.Role);

        return Result.Success(new AuthResponse(
            Token: token,
            RequiresTenantSelection: false,
            AvailableTenants: new List<TenantDto>(),
            UserId: user.Id,
            FullName: user.FullName,
            Email: user.Email
        ));
    }
}
