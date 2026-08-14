using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Contracts;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Domain.Errors;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Tenancy.Application.Features.Login;

public record LoginCommand(string Email, string Password) : IRequest<Result<AuthResponse>>;

public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, Result<AuthResponse>>
{
    private readonly ITenancyDbContext _dbContext;
    private readonly IJwtService _jwtService;

    public LoginCommandHandler(ITenancyDbContext dbContext, IJwtService jwtService)
    {
        _dbContext = dbContext;
        _jwtService = jwtService;
    }

    public async Task<Result<AuthResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .Include(u => u.UserTenants)
            .ThenInclude(ut => ut.Tenant)
            .FirstOrDefaultAsync(u => u.Email == request.Email, cancellationToken);

        if (user == null || !PasswordHasher.Verify(request.Password, user.PasswordHash))
        {
            return Result.Failure<AuthResponse>(
                Error.Unauthorized("Auth.InvalidCredentials", "E-mail ou senha inválidos."));
        }

        var accessibleTenants = user.UserTenants
            .Where(ut => ut.Tenant is not null && TenantLifecycle.CanProducerAccess(ut.Tenant.Status))
            .ToList();

        if (accessibleTenants.Count == 0)
        {
            var hasAnyTenant = user.UserTenants.Count > 0;
            return Result.Failure<AuthResponse>(
                hasAnyTenant
                    ? TenancyErrors.TenantNotAccessible
                    : Error.Failure("Auth.NoTenantAssociation", "Sua conta não está associada a nenhuma granja."));
        }

        if (accessibleTenants.Count == 1)
        {
            var singleTenant = accessibleTenants[0].Tenant!;
            var token = _jwtService.GenerateToken(user, singleTenant, accessibleTenants[0].Role);
            return Result.Success(new AuthResponse(
                Token: token,
                RequiresTenantSelection: false,
                AvailableTenants: new List<TenantDto>(),
                UserId: user.Id,
                FullName: user.FullName,
                Email: user.Email
            ));
        }

        var availableTenants = accessibleTenants
            .Select(ut => new TenantDto(
                ut.Tenant!.Id,
                ut.Tenant.Name,
                ut.Tenant.State,
                ut.Tenant.Type))
            .ToList();

        return Result.Success(new AuthResponse(
            Token: null,
            RequiresTenantSelection: true,
            AvailableTenants: availableTenants,
            UserId: user.Id,
            FullName: user.FullName,
            Email: user.Email
        ));
    }
}
