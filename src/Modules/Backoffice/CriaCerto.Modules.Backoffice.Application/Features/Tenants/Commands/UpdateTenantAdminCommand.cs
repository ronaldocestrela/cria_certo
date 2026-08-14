using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Features.Tenants.Dtos;
using CriaCerto.Modules.Tenancy.Application.Features.BackofficeTenants;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Backoffice.Application.Features.Tenants.Commands;

public record UpdateTenantAdminCommand(
    Guid TenantId,
    string Name,
    string? LegalName,
    string CNPJ,
    string? ExternalIdentifier,
    string State,
    string City,
    string StateRegistration,
    decimal AreaInHectares,
    int Capacity,
    string Type,
    string? TechnicalOwnerName,
    string? TechnicalOwnerEmail,
    string? CommercialOwnerName,
    string? CommercialOwnerEmail,
    Guid PerformedByAdminUserId,
    string PerformedByAdminEmail,
    string IpAddress
) : IRequest<Result<TenantAdminDetailDto>>;

public sealed class UpdateTenantAdminCommandHandler : IRequestHandler<UpdateTenantAdminCommand, Result<TenantAdminDetailDto>>
{
    private readonly ISender _sender;
    private readonly DbContext _dbContext;

    public UpdateTenantAdminCommandHandler(ISender sender, DbContext dbContext)
    {
        _sender = sender;
        _dbContext = dbContext;
    }

    public async Task<Result<TenantAdminDetailDto>> Handle(UpdateTenantAdminCommand request, CancellationToken cancellationToken)
    {
        var beforeResult = await _sender.Send(new GetTenantBackofficeDetailQuery(request.TenantId), cancellationToken);
        if (beforeResult.IsFailure)
        {
            return Result.Failure<TenantAdminDetailDto>(beforeResult.Error);
        }

        var tenancyCommand = new UpdateTenantForAdminCommand(
            request.TenantId,
            request.Name,
            request.LegalName,
            request.CNPJ,
            request.ExternalIdentifier,
            request.State,
            request.City,
            request.StateRegistration,
            request.AreaInHectares,
            request.Capacity,
            request.Type,
            request.TechnicalOwnerName,
            request.TechnicalOwnerEmail,
            request.CommercialOwnerName,
            request.CommercialOwnerEmail);

        var result = await _sender.Send(tenancyCommand, cancellationToken);
        if (result.IsFailure)
        {
            return Result.Failure<TenantAdminDetailDto>(result.Error);
        }

        var after = result.Value;
        var before = beforeResult.Value;

        var auditLog = AuditLog.Create(
            request.PerformedByAdminUserId,
            request.PerformedByAdminEmail,
            "Tenant.Updated",
            $"Tenant/{after.Id}",
            request.IpAddress,
            System.Text.Json.JsonSerializer.Serialize(new
            {
                Before = new
                {
                    before.Name,
                    before.CNPJ,
                    before.ExternalIdentifier,
                    before.Capacity,
                    before.TechnicalOwnerName,
                    before.CommercialOwnerName
                },
                After = new
                {
                    after.Name,
                    after.CNPJ,
                    after.ExternalIdentifier,
                    after.Capacity,
                    after.TechnicalOwnerName,
                    after.CommercialOwnerName
                }
            }));

        _dbContext.Set<AuditLog>().Add(auditLog);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(TenantAdminMapper.ToDetailDto(after));
    }
}
