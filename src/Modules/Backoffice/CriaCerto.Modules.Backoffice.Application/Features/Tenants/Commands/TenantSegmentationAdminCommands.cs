using System.Text.Json;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Features.Tenants.Dtos;
using CriaCerto.Modules.Backoffice.Application.Features.Tenants.Queries;
using CriaCerto.Modules.Tenancy.Application.Contracts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Backoffice.Application.Features.Tenants.Commands;

public record UpdateTenantSegmentationAdminCommand(
    Guid TenantId,
    string SizeSegment,
    string CommercialRegion,
    string ProductiveProfile,
    string ChurnRisk,
    Guid PerformedByAdminUserId,
    string PerformedByAdminEmail,
    string IpAddress
) : IRequest<Result<TenantAdminDetailDto>>;

public sealed class UpdateTenantSegmentationAdminCommandHandler
    : IRequestHandler<UpdateTenantSegmentationAdminCommand, Result<TenantAdminDetailDto>>
{
    private readonly ISender _sender;
    private readonly DbContext _dbContext;

    public UpdateTenantSegmentationAdminCommandHandler(ISender sender, DbContext dbContext)
    {
        _sender = sender;
        _dbContext = dbContext;
    }

    public async Task<Result<TenantAdminDetailDto>> Handle(
        UpdateTenantSegmentationAdminCommand request,
        CancellationToken cancellationToken)
    {
        var beforeResult = await _sender.Send(new Tenancy.Application.Features.BackofficeTenants.GetTenantBackofficeDetailQuery(request.TenantId), cancellationToken);
        if (beforeResult.IsFailure)
        {
            return Result.Failure<TenantAdminDetailDto>(beforeResult.Error);
        }

        var result = await _sender.Send(new Tenancy.Application.Features.BackofficeTenants.UpdateTenantSegmentationForAdminCommand(
            request.TenantId,
            request.SizeSegment,
            request.CommercialRegion,
            request.ProductiveProfile,
            request.ChurnRisk), cancellationToken);

        if (result.IsFailure)
        {
            return Result.Failure<TenantAdminDetailDto>(result.Error);
        }

        var before = beforeResult.Value;
        var after = result.Value;

        var auditLog = AuditLog.Create(
            request.PerformedByAdminUserId,
            request.PerformedByAdminEmail,
            "Tenant.SegmentationUpdated",
            $"Tenant/{after.Id}",
            request.IpAddress,
            JsonSerializer.Serialize(new
            {
                Before = new
                {
                    before.SizeSegment,
                    before.CommercialRegion,
                    before.ProductiveProfile,
                    before.ChurnRisk
                },
                After = new
                {
                    after.SizeSegment,
                    after.CommercialRegion,
                    after.ProductiveProfile,
                    after.ChurnRisk
                }
            }));

        _dbContext.Set<AuditLog>().Add(auditLog);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(TenantAdminMapper.ToDetailDto(after));
    }
}

public record ReplaceTenantTagsAdminCommand(
    Guid TenantId,
    IReadOnlyCollection<Guid> TagIds,
    Guid PerformedByAdminUserId,
    string PerformedByAdminEmail,
    string IpAddress
) : IRequest<Result<TenantAdminDetailDto>>;

public sealed class ReplaceTenantTagsAdminCommandHandler
    : IRequestHandler<ReplaceTenantTagsAdminCommand, Result<TenantAdminDetailDto>>
{
    private readonly ISender _sender;
    private readonly DbContext _dbContext;

    public ReplaceTenantTagsAdminCommandHandler(ISender sender, DbContext dbContext)
    {
        _sender = sender;
        _dbContext = dbContext;
    }

    public async Task<Result<TenantAdminDetailDto>> Handle(
        ReplaceTenantTagsAdminCommand request,
        CancellationToken cancellationToken)
    {
        var beforeResult = await _sender.Send(new Tenancy.Application.Features.BackofficeTenants.GetTenantBackofficeDetailQuery(request.TenantId), cancellationToken);
        if (beforeResult.IsFailure)
        {
            return Result.Failure<TenantAdminDetailDto>(beforeResult.Error);
        }

        var result = await _sender.Send(new Tenancy.Application.Features.BackofficeTenants.ReplaceTenantTagsForAdminCommand(
            request.TenantId,
            request.TagIds), cancellationToken);

        if (result.IsFailure)
        {
            return Result.Failure<TenantAdminDetailDto>(result.Error);
        }

        var auditLog = AuditLog.Create(
            request.PerformedByAdminUserId,
            request.PerformedByAdminEmail,
            "Tenant.TagsReplaced",
            $"Tenant/{request.TenantId}",
            request.IpAddress,
            JsonSerializer.Serialize(new
            {
                Before = beforeResult.Value.Tags.Select(t => t.Slug).ToList(),
                After = result.Value.Tags.Select(t => t.Slug).ToList()
            }));

        _dbContext.Set<AuditLog>().Add(auditLog);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(TenantAdminMapper.ToDetailDto(result.Value));
    }
}

public record CreateOperationalTagAdminCommand(
    string Name,
    string Category,
    string? ColorHex,
    Guid PerformedByAdminUserId,
    string PerformedByAdminEmail,
    string IpAddress
) : IRequest<Result<OperationalTagAdminDto>>;

public sealed class CreateOperationalTagAdminCommandHandler
    : IRequestHandler<CreateOperationalTagAdminCommand, Result<OperationalTagAdminDto>>
{
    private readonly ISender _sender;
    private readonly DbContext _dbContext;

    public CreateOperationalTagAdminCommandHandler(ISender sender, DbContext dbContext)
    {
        _sender = sender;
        _dbContext = dbContext;
    }

    public async Task<Result<OperationalTagAdminDto>> Handle(
        CreateOperationalTagAdminCommand request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new Tenancy.Application.Features.BackofficeTenants.CreateOperationalTagForAdminCommand(
            request.Name,
            request.Category,
            request.ColorHex), cancellationToken);

        if (result.IsFailure)
        {
            return Result.Failure<OperationalTagAdminDto>(result.Error);
        }

        var tag = result.Value;
        var auditLog = AuditLog.Create(
            request.PerformedByAdminUserId,
            request.PerformedByAdminEmail,
            "Tenant.TagCreated",
            $"OperationalTag/{tag.Id}",
            request.IpAddress,
            JsonSerializer.Serialize(new { tag.Slug, tag.Name, tag.Category }));

        _dbContext.Set<AuditLog>().Add(auditLog);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(TenantAdminMapper.ToTagDto(tag));
    }
}

public record DeactivateOperationalTagAdminCommand(
    Guid TagId,
    Guid PerformedByAdminUserId,
    string PerformedByAdminEmail,
    string IpAddress
) : IRequest<Result<OperationalTagAdminDto>>;

public sealed class DeactivateOperationalTagAdminCommandHandler
    : IRequestHandler<DeactivateOperationalTagAdminCommand, Result<OperationalTagAdminDto>>
{
    private readonly ISender _sender;
    private readonly DbContext _dbContext;

    public DeactivateOperationalTagAdminCommandHandler(ISender sender, DbContext dbContext)
    {
        _sender = sender;
        _dbContext = dbContext;
    }

    public async Task<Result<OperationalTagAdminDto>> Handle(
        DeactivateOperationalTagAdminCommand request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new Tenancy.Application.Features.BackofficeTenants.DeactivateOperationalTagForAdminCommand(request.TagId), cancellationToken);
        if (result.IsFailure)
        {
            return Result.Failure<OperationalTagAdminDto>(result.Error);
        }

        var tag = result.Value;
        var auditLog = AuditLog.Create(
            request.PerformedByAdminUserId,
            request.PerformedByAdminEmail,
            "Tenant.TagDeactivated",
            $"OperationalTag/{tag.Id}",
            request.IpAddress,
            JsonSerializer.Serialize(new { tag.Slug, tag.Name }));

        _dbContext.Set<AuditLog>().Add(auditLog);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(TenantAdminMapper.ToTagDto(tag));
    }
}

public record ExportTenantsAdminAuditCommand(
    ExportTenantsAdminQuery Query,
    Guid PerformedByAdminUserId,
    string PerformedByAdminEmail,
    string IpAddress,
    TenantExportResultDto ExportResult
) : IRequest<Result>;

public sealed class ExportTenantsAdminAuditCommandHandler : IRequestHandler<ExportTenantsAdminAuditCommand, Result>
{
    private readonly DbContext _dbContext;

    public ExportTenantsAdminAuditCommandHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result> Handle(ExportTenantsAdminAuditCommand request, CancellationToken cancellationToken)
    {
        var auditLog = AuditLog.Create(
            request.PerformedByAdminUserId,
            request.PerformedByAdminEmail,
            "Tenant.Exported",
            "Tenant/Export",
            request.IpAddress,
            JsonSerializer.Serialize(new
            {
                request.ExportResult.RowCount,
                request.ExportResult.FileName,
                Filters = request.Query
            }));

        _dbContext.Set<AuditLog>().Add(auditLog);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
