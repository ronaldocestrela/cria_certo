using System.Text.Json;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;
using CriaCerto.Modules.Backoffice.Application.Features.Plans.Dtos;
using CriaCerto.Modules.Backoffice.Application.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Backoffice.Application.Features.Plans.Commands;

public record CreateModuleCatalogCommand(
    string ModuleKey,
    string Name,
    string Description,
    string Icon,
    bool IsCore,
    int SortOrder,
    bool IsActive,
    List<ModuleTierPriceInputDto>? InitialTierPrices,
    Guid PerformedByAdminUserId,
    string PerformedByAdminEmail,
    string IpAddress
) : IRequest<Result<ModuleCatalogDto>>;

public record UpdateModuleCatalogCommand(
    Guid ModuleId,
    string Name,
    string Description,
    string Icon,
    bool IsCore,
    int SortOrder,
    bool IsActive,
    Guid PerformedByAdminUserId,
    string PerformedByAdminEmail,
    string IpAddress
) : IRequest<Result<ModuleCatalogDto>>;

public record CreateModuleTierPriceDraftCommand(
    Guid ModuleCatalogId,
    Guid HeadTierCatalogId,
    decimal MonthlyPrice,
    decimal AnnualPriceMonthly,
    string? StripePriceIdMonthly,
    string? StripePriceIdAnnual,
    Guid PerformedByAdminUserId,
    string PerformedByAdminEmail,
    string IpAddress
) : IRequest<Result<ModuleTierPriceDto>>;

public record UpdateModuleTierPriceDraftCommand(
    Guid PriceId,
    decimal MonthlyPrice,
    decimal AnnualPriceMonthly,
    string? StripePriceIdMonthly,
    string? StripePriceIdAnnual,
    Guid PerformedByAdminUserId,
    string PerformedByAdminEmail,
    string IpAddress
) : IRequest<Result<ModuleTierPriceDto>>;

[RequireFeatureFlag("backoffice.feature.plan_publishing")]
public record PublishModuleTierPriceCommand(
    Guid PriceId,
    string? ApprovalNotes,
    Guid PerformedByAdminUserId,
    string PerformedByAdminEmail,
    string IpAddress,
    string? ActorRole = null
) : IRequest<Result<ModuleTierPriceDto>>, IBackofficeActorRequest
{
    public Guid ActorId => PerformedByAdminUserId;
    public string ActorEmail => PerformedByAdminEmail;
}

public sealed class CreateModuleCatalogCommandHandler : IRequestHandler<CreateModuleCatalogCommand, Result<ModuleCatalogDto>>
{
    private readonly DbContext _dbContext;

    public CreateModuleCatalogCommandHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<ModuleCatalogDto>> Handle(CreateModuleCatalogCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ModuleKey))
        {
            return Result.Failure<ModuleCatalogDto>(PlanErrors.InvalidModuleCatalogData);
        }

        var normalizedKey = request.ModuleKey.Trim();
        var exists = await _dbContext.Set<ModuleCatalog>()
            .AnyAsync(m => m.ModuleKey == normalizedKey, cancellationToken);

        if (exists)
        {
            return Result.Failure<ModuleCatalogDto>(PlanErrors.ModuleKeyAlreadyExists);
        }

        var moduleResult = ModuleCatalog.Create(
            normalizedKey,
            request.Name,
            request.Description,
            request.Icon,
            request.IsCore,
            request.SortOrder,
            request.IsActive);

        if (moduleResult.IsFailure)
        {
            return Result.Failure<ModuleCatalogDto>(moduleResult.Error);
        }

        var module = moduleResult.Value;

        var tiersMap = await _dbContext.Set<HeadTierCatalog>()
            .AsNoTracking()
            .ToDictionaryAsync(t => t.Id, t => t.TierCode, cancellationToken);

        if (request.InitialTierPrices is not null)
        {
            foreach (var priceInput in request.InitialTierPrices)
            {
                if (!tiersMap.ContainsKey(priceInput.HeadTierCatalogId))
                {
                    return Result.Failure<ModuleCatalogDto>(PlanErrors.HeadTierNotFound);
                }

                var draftResult = module.CreateTierPriceDraft(
                    priceInput.HeadTierCatalogId,
                    priceInput.MonthlyPrice,
                    priceInput.AnnualPriceMonthly,
                    priceInput.StripePriceIdMonthly,
                    priceInput.StripePriceIdAnnual);

                if (draftResult.IsFailure)
                {
                    return Result.Failure<ModuleCatalogDto>(draftResult.Error);
                }
            }
        }

        _dbContext.Set<ModuleCatalog>().Add(module);

        var auditLog = AuditLog.Create(
            request.PerformedByAdminUserId,
            request.PerformedByAdminEmail,
            "ModuleCatalog.Created",
            $"ModuleCatalog/{module.Id}",
            request.IpAddress,
            JsonSerializer.Serialize(new
            {
                module.Id,
                module.ModuleKey,
                module.Name,
                module.IsCore,
                InitialTierPricesCount = module.TierPrices.Count
            }));

        _dbContext.Set<AuditLog>().Add(auditLog);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(module.ToDto(tiersMap));
    }
}

public sealed class UpdateModuleCatalogCommandHandler : IRequestHandler<UpdateModuleCatalogCommand, Result<ModuleCatalogDto>>
{
    private readonly DbContext _dbContext;

    public UpdateModuleCatalogCommandHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<ModuleCatalogDto>> Handle(UpdateModuleCatalogCommand request, CancellationToken cancellationToken)
    {
        var module = await _dbContext.Set<ModuleCatalog>()
            .Include(m => m.TierPrices)
            .FirstOrDefaultAsync(m => m.Id == request.ModuleId, cancellationToken);

        if (module is null)
        {
            return Result.Failure<ModuleCatalogDto>(PlanErrors.ModuleCatalogNotFound);
        }

        var updateResult = module.UpdateDetails(
            request.Name,
            request.Description,
            request.Icon,
            request.IsCore,
            request.SortOrder,
            request.IsActive);

        if (updateResult.IsFailure)
        {
            return Result.Failure<ModuleCatalogDto>(updateResult.Error);
        }

        var auditLog = AuditLog.Create(
            request.PerformedByAdminUserId,
            request.PerformedByAdminEmail,
            "ModuleCatalog.Updated",
            $"ModuleCatalog/{module.Id}",
            request.IpAddress,
            JsonSerializer.Serialize(new
            {
                module.Id,
                module.ModuleKey,
                module.Name,
                module.IsCore,
                module.IsActive,
                module.SortOrder
            }));

        _dbContext.Set<AuditLog>().Add(auditLog);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var tiersMap = await _dbContext.Set<HeadTierCatalog>()
            .AsNoTracking()
            .ToDictionaryAsync(t => t.Id, t => t.TierCode, cancellationToken);

        return Result.Success(module.ToDto(tiersMap));
    }
}

public sealed class CreateModuleTierPriceDraftCommandHandler : IRequestHandler<CreateModuleTierPriceDraftCommand, Result<ModuleTierPriceDto>>
{
    private readonly DbContext _dbContext;

    public CreateModuleTierPriceDraftCommandHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<ModuleTierPriceDto>> Handle(CreateModuleTierPriceDraftCommand request, CancellationToken cancellationToken)
    {
        var module = await _dbContext.Set<ModuleCatalog>()
            .Include(m => m.TierPrices)
            .FirstOrDefaultAsync(m => m.Id == request.ModuleCatalogId, cancellationToken);

        if (module is null)
        {
            return Result.Failure<ModuleTierPriceDto>(PlanErrors.ModuleCatalogNotFound);
        }

        var tier = await _dbContext.Set<HeadTierCatalog>()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.HeadTierCatalogId, cancellationToken);

        if (tier is null)
        {
            return Result.Failure<ModuleTierPriceDto>(PlanErrors.HeadTierNotFound);
        }

        var draftResult = module.CreateTierPriceDraft(
            tier.Id,
            request.MonthlyPrice,
            request.AnnualPriceMonthly,
            request.StripePriceIdMonthly,
            request.StripePriceIdAnnual);

        if (draftResult.IsFailure)
        {
            return Result.Failure<ModuleTierPriceDto>(draftResult.Error);
        }

        var draft = draftResult.Value;
        _dbContext.Set<ModuleTierPrice>().Add(draft);

        var auditLog = AuditLog.Create(
            request.PerformedByAdminUserId,
            request.PerformedByAdminEmail,
            "ModuleTierPrice.DraftCreated",
            $"ModuleTierPrice/{draft.Id}",
            request.IpAddress,
            JsonSerializer.Serialize(new
            {
                draft.Id,
                draft.ModuleCatalogId,
                module.ModuleKey,
                draft.HeadTierCatalogId,
                tier.TierCode,
                draft.VersionNumber,
                draft.MonthlyPrice,
                draft.AnnualPriceMonthly
            }));

        _dbContext.Set<AuditLog>().Add(auditLog);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(draft.ToDto(tier.TierCode));
    }
}

public sealed class UpdateModuleTierPriceDraftCommandHandler : IRequestHandler<UpdateModuleTierPriceDraftCommand, Result<ModuleTierPriceDto>>
{
    private readonly DbContext _dbContext;

    public UpdateModuleTierPriceDraftCommandHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<ModuleTierPriceDto>> Handle(UpdateModuleTierPriceDraftCommand request, CancellationToken cancellationToken)
    {
        var price = await _dbContext.Set<ModuleTierPrice>()
            .FirstOrDefaultAsync(p => p.Id == request.PriceId, cancellationToken);

        if (price is null)
        {
            return Result.Failure<ModuleTierPriceDto>(PlanErrors.ModuleTierPriceNotFound);
        }

        var updateResult = price.UpdateDraft(
            request.MonthlyPrice,
            request.AnnualPriceMonthly,
            request.StripePriceIdMonthly,
            request.StripePriceIdAnnual);

        if (updateResult.IsFailure)
        {
            return Result.Failure<ModuleTierPriceDto>(updateResult.Error);
        }

        var tier = await _dbContext.Set<HeadTierCatalog>()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == price.HeadTierCatalogId, cancellationToken);

        var auditLog = AuditLog.Create(
            request.PerformedByAdminUserId,
            request.PerformedByAdminEmail,
            "ModuleTierPrice.DraftUpdated",
            $"ModuleTierPrice/{price.Id}",
            request.IpAddress,
            JsonSerializer.Serialize(new
            {
                price.Id,
                price.ModuleCatalogId,
                price.HeadTierCatalogId,
                price.VersionNumber,
                price.MonthlyPrice,
                price.AnnualPriceMonthly,
                price.StripePriceIdMonthly,
                price.StripePriceIdAnnual
            }));

        _dbContext.Set<AuditLog>().Add(auditLog);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(price.ToDto(tier?.TierCode ?? string.Empty));
    }
}

public sealed class PublishModuleTierPriceCommandHandler : IRequestHandler<PublishModuleTierPriceCommand, Result<ModuleTierPriceDto>>
{
    private readonly DbContext _dbContext;

    public PublishModuleTierPriceCommandHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<ModuleTierPriceDto>> Handle(PublishModuleTierPriceCommand request, CancellationToken cancellationToken)
    {
        var module = await _dbContext.Set<ModuleCatalog>()
            .Include(m => m.TierPrices)
            .FirstOrDefaultAsync(m => m.TierPrices.Any(p => p.Id == request.PriceId), cancellationToken);

        if (module is null)
        {
            return Result.Failure<ModuleTierPriceDto>(PlanErrors.ModuleTierPriceNotFound);
        }

        var price = module.TierPrices.FirstOrDefault(p => p.Id == request.PriceId);
        if (price is null)
        {
            return Result.Failure<ModuleTierPriceDto>(PlanErrors.ModuleTierPriceNotFound);
        }

        var publishResult = module.PublishTierPrice(price.Id, request.PerformedByAdminUserId, request.ApprovalNotes);
        if (publishResult.IsFailure)
        {
            return Result.Failure<ModuleTierPriceDto>(publishResult.Error);
        }

        var tier = await _dbContext.Set<HeadTierCatalog>()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == price.HeadTierCatalogId, cancellationToken);

        var auditLog = AuditLog.Create(
            request.PerformedByAdminUserId,
            request.PerformedByAdminEmail,
            "ModuleTierPrice.Published",
            $"ModuleTierPrice/{price.Id}",
            request.IpAddress,
            JsonSerializer.Serialize(new
            {
                price.Id,
                price.ModuleCatalogId,
                module.ModuleKey,
                price.HeadTierCatalogId,
                TierCode = tier?.TierCode,
                price.VersionNumber,
                price.MonthlyPrice,
                price.AnnualPriceMonthly,
                PublishedByAdminId = request.PerformedByAdminUserId,
                request.ApprovalNotes
            }));

        _dbContext.Set<AuditLog>().Add(auditLog);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(price.ToDto(tier?.TierCode ?? string.Empty));
    }
}

