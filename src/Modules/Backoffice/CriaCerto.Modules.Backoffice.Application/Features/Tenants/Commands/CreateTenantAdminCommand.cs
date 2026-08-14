using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Features.Tenants.Dtos;
using CriaCerto.Modules.Tenancy.Application.Features.BackofficeTenants;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Backoffice.Application.Features.Tenants.Commands;

public record CreateTenantAdminCommand(
    string Name,
    string? LegalName,
    string CNPJ,
    string? ExternalIdentifier,
    string State,
    string City,
    string StateRegistration,
    decimal AreaInHectares,
    string SubscribedPlan,
    int Capacity,
    string Type,
    string? TechnicalOwnerName,
    string? TechnicalOwnerEmail,
    string? CommercialOwnerName,
    string? CommercialOwnerEmail,
    string? OwnerUserEmail,
    Guid PerformedByAdminUserId,
    string PerformedByAdminEmail,
    string IpAddress
) : IRequest<Result<TenantAdminDetailDto>>;

public sealed class CreateTenantAdminCommandHandler : IRequestHandler<CreateTenantAdminCommand, Result<TenantAdminDetailDto>>
{
    private readonly ISender _sender;
    private readonly DbContext _dbContext;

    public CreateTenantAdminCommandHandler(ISender sender, DbContext dbContext)
    {
        _sender = sender;
        _dbContext = dbContext;
    }

    public async Task<Result<TenantAdminDetailDto>> Handle(CreateTenantAdminCommand request, CancellationToken cancellationToken)
    {
        var tenancyCommand = new CreateTenantForAdminCommand(
            request.Name,
            request.LegalName,
            request.CNPJ,
            request.ExternalIdentifier,
            request.State,
            request.City,
            request.StateRegistration,
            request.AreaInHectares,
            request.SubscribedPlan,
            request.Capacity,
            request.Type,
            request.TechnicalOwnerName,
            request.TechnicalOwnerEmail,
            request.CommercialOwnerName,
            request.CommercialOwnerEmail,
            request.OwnerUserEmail);

        var result = await _sender.Send(tenancyCommand, cancellationToken);
        if (result.IsFailure)
        {
            return Result.Failure<TenantAdminDetailDto>(result.Error);
        }

        var tenant = result.Value;
        var auditLog = AuditLog.Create(
            request.PerformedByAdminUserId,
            request.PerformedByAdminEmail,
            "Tenant.Created",
            $"Tenant/{tenant.Id}",
            request.IpAddress,
            System.Text.Json.JsonSerializer.Serialize(new
            {
                tenant.Id,
                tenant.Name,
                tenant.CNPJ,
                tenant.ExternalIdentifier,
                tenant.SubscribedPlan
            }));

        _dbContext.Set<AuditLog>().Add(auditLog);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(TenantAdminMapper.ToDetailDto(tenant));
    }
}
