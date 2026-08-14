using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Contracts;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Domain.Errors;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Tenancy.Application.Features.UpdateTenantProfile;

public record UpdateTenantProfileCommand(
    Guid TenantId,
    string Name,
    string CNPJ,
    string State,
    string City,
    string StateRegistration,
    decimal AreaInHectares,
    int Capacity,
    string Type
) : IRequest<Result<TenantProfileDto>>;

public class UpdateTenantProfileCommandHandler : IRequestHandler<UpdateTenantProfileCommand, Result<TenantProfileDto>>
{
    private readonly ITenancyDbContext _dbContext;

    public UpdateTenantProfileCommandHandler(ITenancyDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<TenantProfileDto>> Handle(UpdateTenantProfileCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Id == request.TenantId, cancellationToken);

        if (tenant is null)
        {
            return Result.Failure<TenantProfileDto>(TenancyErrors.TenantNotFound);
        }

        var cnpjNormalized = CnpjNormalizer.Normalize(request.CNPJ);
        if (!CnpjNormalizer.IsValidCnpjOrCpf(request.CNPJ))
        {
            return Result.Failure<TenantProfileDto>(TenancyErrors.InvalidCnpj);
        }

        var cnpjConflict = await _dbContext.Tenants
            .AnyAsync(t => t.CnpjNormalized == cnpjNormalized && t.Id != request.TenantId, cancellationToken);
        if (cnpjConflict)
        {
            return Result.Failure<TenantProfileDto>(TenancyErrors.CnpjAlreadyExists);
        }

        tenant.Name = request.Name;
        tenant.CNPJ = request.CNPJ;
        tenant.CnpjNormalized = cnpjNormalized;
        tenant.State = request.State;
        tenant.City = request.City;
        tenant.StateRegistration = request.StateRegistration;
        tenant.AreaInHectares = request.AreaInHectares;
        tenant.Capacity = request.Capacity;
        tenant.Type = request.Type;
        tenant.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var dto = new TenantProfileDto(
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

        return Result.Success(dto);
    }
}
