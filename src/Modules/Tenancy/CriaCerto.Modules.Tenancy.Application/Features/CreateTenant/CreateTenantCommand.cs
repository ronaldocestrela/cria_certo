using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.BuildingBlocks.Abstractions.Tenancy;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Contracts;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Domain.Errors;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Tenancy.Application.Features.CreateTenant;

public record CreateTenantCommand(
    Guid? UserId,
    string Name,
    string CNPJ,
    string State,
    string City,
    string StateRegistration,
    decimal AreaInHectares,
    string SubscribedPlan,
    int Capacity,
    string? UserEmail = null
) : IRequest<Result<AuthResponse>>;

public sealed class CreateTenantCommandHandler : IRequestHandler<CreateTenantCommand, Result<AuthResponse>>
{
    private readonly ITenancyDbContext _dbContext;
    private readonly IJwtService _jwtService;
    private readonly ITenantDatabaseProvisioner _provisioner;

    public CreateTenantCommandHandler(ITenancyDbContext dbContext, IJwtService jwtService, ITenantDatabaseProvisioner provisioner)
    {
        _dbContext = dbContext;
        _jwtService = jwtService;
        _provisioner = provisioner;
    }

    public async Task<Result<AuthResponse>> Handle(CreateTenantCommand request, CancellationToken cancellationToken)
    {
        User? user = null;

        if (request.UserId.HasValue && request.UserId.Value != Guid.Empty)
        {
            user = await _dbContext.Users
                .Include(u => u.UserTenants)
                .FirstOrDefaultAsync(u => u.Id == request.UserId.Value, cancellationToken);
        }

        if (user == null && !string.IsNullOrWhiteSpace(request.UserEmail))
        {
            var normalizedEmail = request.UserEmail.Trim().ToLowerInvariant();
            user = await _dbContext.Users
                .Include(u => u.UserTenants)
                .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);
        }

        if (user == null)
        {
            return Result.Failure<AuthResponse>(
                Error.NotFound("User.NotFound", "Usuário não encontrado para criação da fazenda. Por favor, faça o cadastro novamente."));
        }

        var plan = string.IsNullOrWhiteSpace(request.SubscribedPlan) ? "Starter" : request.SubscribedPlan.Trim();
        var capacity = request.Capacity > 0 ? request.Capacity : 1000;
        var cnpjNormalized = CnpjNormalizer.Normalize(request.CNPJ);

        if (!CnpjNormalizer.IsValidCnpjOrCpf(request.CNPJ))
        {
            return Result.Failure<AuthResponse>(TenancyErrors.InvalidCnpj);
        }

        var cnpjExists = await _dbContext.Tenants
            .AnyAsync(t => t.CnpjNormalized == cnpjNormalized, cancellationToken);
        if (cnpjExists)
        {
            return Result.Failure<AuthResponse>(TenancyErrors.CnpjAlreadyExists);
        }

        var now = DateTime.UtcNow;
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            CNPJ = request.CNPJ?.Trim() ?? string.Empty,
            CnpjNormalized = cnpjNormalized,
            State = request.State?.Trim().ToUpperInvariant() ?? string.Empty,
            City = request.City?.Trim() ?? string.Empty,
            StateRegistration = request.StateRegistration?.Trim() ?? string.Empty,
            AreaInHectares = request.AreaInHectares,
            SubscribedPlan = plan,
            Capacity = capacity,
            Status = "Active",
            Type = "Pecuária de Corte e Cria",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        var userTenant = new UserTenant
        {
            UserId = user.Id,
            User = user,
            TenantId = tenant.Id,
            Tenant = tenant
        };

        _dbContext.Tenants.Add(tenant);
        _dbContext.UserTenants.Add(userTenant);

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _provisioner.EnsureTenantDatabaseAsync(tenant.Id, cancellationToken);

        var token = _jwtService.GenerateToken(user, tenant);

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
