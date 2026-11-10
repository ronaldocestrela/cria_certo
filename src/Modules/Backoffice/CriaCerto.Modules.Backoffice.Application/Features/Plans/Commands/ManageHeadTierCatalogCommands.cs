using System.Text.Json;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;
using CriaCerto.Modules.Backoffice.Application.Features.Plans.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Backoffice.Application.Features.Plans.Commands;

public record CreateHeadTierCatalogCommand(
    string TierCode,
    string DisplayName,
    int MinHeads,
    int MaxHeads,
    int SortOrder,
    bool IsActive,
    Guid PerformedByAdminUserId,
    string PerformedByAdminEmail,
    string IpAddress
) : IRequest<Result<HeadTierCatalogDto>>;

public record UpdateHeadTierCatalogCommand(
    Guid TierId,
    string DisplayName,
    int MinHeads,
    int MaxHeads,
    int SortOrder,
    bool IsActive,
    Guid PerformedByAdminUserId,
    string PerformedByAdminEmail,
    string IpAddress
) : IRequest<Result<HeadTierCatalogDto>>;

public sealed class CreateHeadTierCatalogCommandHandler : IRequestHandler<CreateHeadTierCatalogCommand, Result<HeadTierCatalogDto>>
{
    private readonly DbContext _dbContext;

    public CreateHeadTierCatalogCommandHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<HeadTierCatalogDto>> Handle(CreateHeadTierCatalogCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TierCode))
        {
            return Result.Failure<HeadTierCatalogDto>(PlanErrors.InvalidHeadTierData);
        }

        var normalizedCode = request.TierCode.Trim().ToUpperInvariant();
        var exists = await _dbContext.Set<HeadTierCatalog>()
            .AnyAsync(t => t.TierCode == normalizedCode, cancellationToken);

        if (exists)
        {
            return Result.Failure<HeadTierCatalogDto>(PlanErrors.HeadTierCodeAlreadyExists);
        }

        var tierResult = HeadTierCatalog.Create(
            normalizedCode,
            request.DisplayName,
            request.MinHeads,
            request.MaxHeads,
            request.SortOrder,
            request.IsActive);

        if (tierResult.IsFailure)
        {
            return Result.Failure<HeadTierCatalogDto>(tierResult.Error);
        }

        var tier = tierResult.Value;
        _dbContext.Set<HeadTierCatalog>().Add(tier);

        var auditLog = AuditLog.Create(
            request.PerformedByAdminUserId,
            request.PerformedByAdminEmail,
            "HeadTierCatalog.Created",
            $"HeadTierCatalog/{tier.Id}",
            request.IpAddress,
            JsonSerializer.Serialize(new
            {
                tier.Id,
                tier.TierCode,
                tier.DisplayName,
                tier.MinHeads,
                tier.MaxHeads,
                tier.SortOrder,
                tier.IsActive
            }));

        _dbContext.Set<AuditLog>().Add(auditLog);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(tier.ToDto());
    }
}

public sealed class UpdateHeadTierCatalogCommandHandler : IRequestHandler<UpdateHeadTierCatalogCommand, Result<HeadTierCatalogDto>>
{
    private readonly DbContext _dbContext;

    public UpdateHeadTierCatalogCommandHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<HeadTierCatalogDto>> Handle(UpdateHeadTierCatalogCommand request, CancellationToken cancellationToken)
    {
        var tier = await _dbContext.Set<HeadTierCatalog>()
            .FirstOrDefaultAsync(t => t.Id == request.TierId, cancellationToken);

        if (tier is null)
        {
            return Result.Failure<HeadTierCatalogDto>(PlanErrors.HeadTierNotFound);
        }

        var updateResult = tier.UpdateDetails(
            request.DisplayName,
            request.MinHeads,
            request.MaxHeads,
            request.SortOrder,
            request.IsActive);

        if (updateResult.IsFailure)
        {
            return Result.Failure<HeadTierCatalogDto>(updateResult.Error);
        }

        var auditLog = AuditLog.Create(
            request.PerformedByAdminUserId,
            request.PerformedByAdminEmail,
            "HeadTierCatalog.Updated",
            $"HeadTierCatalog/{tier.Id}",
            request.IpAddress,
            JsonSerializer.Serialize(new
            {
                tier.Id,
                tier.TierCode,
                tier.DisplayName,
                tier.MinHeads,
                tier.MaxHeads,
                tier.SortOrder,
                tier.IsActive
            }));

        _dbContext.Set<AuditLog>().Add(auditLog);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(tier.ToDto());
    }
}

