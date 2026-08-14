using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Contracts;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Domain.Errors;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Tenancy.Application.Features.BackofficeTenants;

public record UpdateTenantSegmentationForAdminCommand(
    Guid TenantId,
    string SizeSegment,
    string CommercialRegion,
    string ProductiveProfile,
    string ChurnRisk
) : IRequest<Result<TenantBackofficeDetailDto>>;

public sealed class UpdateTenantSegmentationForAdminCommandValidator : AbstractValidator<UpdateTenantSegmentationForAdminCommand>
{
    public UpdateTenantSegmentationForAdminCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.SizeSegment).Must(v => TenantSegmentationCatalog.ValidateSizeSegment(v).IsSuccess);
        RuleFor(x => x.CommercialRegion).Must(v => TenantSegmentationCatalog.ValidateCommercialRegion(v).IsSuccess);
        RuleFor(x => x.ProductiveProfile).Must(v => TenantSegmentationCatalog.ValidateProductiveProfile(v).IsSuccess);
        RuleFor(x => x.ChurnRisk).Must(v => TenantSegmentationCatalog.ValidateChurnRisk(v).IsSuccess);
    }
}

public sealed class UpdateTenantSegmentationForAdminCommandHandler
    : IRequestHandler<UpdateTenantSegmentationForAdminCommand, Result<TenantBackofficeDetailDto>>
{
    private readonly ITenancyDbContext _dbContext;

    public UpdateTenantSegmentationForAdminCommandHandler(ITenancyDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<TenantBackofficeDetailDto>> Handle(
        UpdateTenantSegmentationForAdminCommand request,
        CancellationToken cancellationToken)
    {
        var tenant = await _dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Id == request.TenantId, cancellationToken);

        if (tenant is null)
        {
            return Result.Failure<TenantBackofficeDetailDto>(TenancyErrors.TenantNotFound);
        }

        var updateResult = tenant.UpdateSegmentation(
            request.SizeSegment,
            request.CommercialRegion,
            request.ProductiveProfile,
            request.ChurnRisk);

        if (updateResult.IsFailure)
        {
            return Result.Failure<TenantBackofficeDetailDto>(updateResult.Error);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var tags = await LoadTenantTagsAsync(tenant.Id, cancellationToken);
        var teamCount = await _dbContext.UserTenants.CountAsync(ut => ut.TenantId == tenant.Id, cancellationToken);
        var unitCount = await _dbContext.ProductionUnits.CountAsync(pu => pu.TenantId == tenant.Id, cancellationToken);

        return Result.Success(TenantBackofficeMapper.ToDetailDto(tenant, teamCount, unitCount, tags));
    }

    private async Task<IReadOnlyCollection<TenantOperationalTagDto>> LoadTenantTagsAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await _dbContext.TenantOperationalTags
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.Tag.IsActive)
            .Select(t => new TenantOperationalTagDto(t.Tag.Id, t.Tag.Slug, t.Tag.Name, t.Tag.ColorHex, t.Tag.Category))
            .ToListAsync(cancellationToken);
}

public record ReplaceTenantTagsForAdminCommand(
    Guid TenantId,
    IReadOnlyCollection<Guid> TagIds
) : IRequest<Result<TenantBackofficeDetailDto>>;

public sealed class ReplaceTenantTagsForAdminCommandHandler
    : IRequestHandler<ReplaceTenantTagsForAdminCommand, Result<TenantBackofficeDetailDto>>
{
    private readonly ITenancyDbContext _dbContext;

    public ReplaceTenantTagsForAdminCommandHandler(ITenancyDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<TenantBackofficeDetailDto>> Handle(
        ReplaceTenantTagsForAdminCommand request,
        CancellationToken cancellationToken)
    {
        var tenant = await _dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Id == request.TenantId, cancellationToken);

        if (tenant is null)
        {
            return Result.Failure<TenantBackofficeDetailDto>(TenancyErrors.TenantNotFound);
        }

        var distinctTagIds = request.TagIds.Distinct().ToList();
        if (distinctTagIds.Count > 0)
        {
            var tags = await _dbContext.OperationalTags
                .Where(t => distinctTagIds.Contains(t.Id))
                .ToListAsync(cancellationToken);

            if (tags.Count != distinctTagIds.Count)
            {
                return Result.Failure<TenantBackofficeDetailDto>(TenancyErrors.TagNotFound);
            }

            if (tags.Any(t => !t.IsActive))
            {
                return Result.Failure<TenantBackofficeDetailDto>(TenancyErrors.TagInactive);
            }
        }

        var existing = await _dbContext.TenantOperationalTags
            .Where(t => t.TenantId == request.TenantId)
            .ToListAsync(cancellationToken);

        _dbContext.TenantOperationalTags.RemoveRange(existing);

        foreach (var tagId in distinctTagIds)
        {
            _dbContext.TenantOperationalTags.Add(new TenantOperationalTag
            {
                TenantId = request.TenantId,
                TagId = tagId,
                AssignedAtUtc = DateTime.UtcNow
            });
        }

        tenant.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var assignedTags = await _dbContext.TenantOperationalTags
            .AsNoTracking()
            .Where(t => t.TenantId == tenant.Id && t.Tag.IsActive)
            .Select(t => new TenantOperationalTagDto(t.Tag.Id, t.Tag.Slug, t.Tag.Name, t.Tag.ColorHex, t.Tag.Category))
            .ToListAsync(cancellationToken);

        var teamCount = await _dbContext.UserTenants.CountAsync(ut => ut.TenantId == tenant.Id, cancellationToken);
        var unitCount = await _dbContext.ProductionUnits.CountAsync(pu => pu.TenantId == tenant.Id, cancellationToken);

        return Result.Success(TenantBackofficeMapper.ToDetailDto(tenant, teamCount, unitCount, assignedTags));
    }
}

public record CreateOperationalTagForAdminCommand(
    string Name,
    string Category,
    string? ColorHex
) : IRequest<Result<OperationalTagDto>>;

public sealed class CreateOperationalTagForAdminCommandValidator : AbstractValidator<CreateOperationalTagForAdminCommand>
{
    public CreateOperationalTagForAdminCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Category).Must(v => TenantSegmentationCatalog.ValidateTagCategory(v).IsSuccess);
        RuleFor(x => x.ColorHex).Matches("^#[0-9A-Fa-f]{6}$").When(x => !string.IsNullOrWhiteSpace(x.ColorHex));
    }
}

public sealed class CreateOperationalTagForAdminCommandHandler
    : IRequestHandler<CreateOperationalTagForAdminCommand, Result<OperationalTagDto>>
{
    private readonly ITenancyDbContext _dbContext;

    public CreateOperationalTagForAdminCommandHandler(ITenancyDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<OperationalTagDto>> Handle(
        CreateOperationalTagForAdminCommand request,
        CancellationToken cancellationToken)
    {
        var slug = TenantSegmentationCatalog.GenerateTagSlug(request.Name);
        if (string.IsNullOrWhiteSpace(slug))
        {
            return Result.Failure<OperationalTagDto>(TenancyErrors.InvalidSegmentation);
        }

        var slugExists = await _dbContext.OperationalTags
            .AnyAsync(t => t.Slug == slug, cancellationToken);

        if (slugExists)
        {
            return Result.Failure<OperationalTagDto>(TenancyErrors.TagSlugAlreadyExists);
        }

        var tag = new OperationalTag
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Slug = slug,
            Category = TenantSegmentationCatalog.NormalizeSegmentValue(request.Category, TenantSegmentationCatalog.TagCategories.All),
            ColorHex = string.IsNullOrWhiteSpace(request.ColorHex) ? "#6366f1" : request.ColorHex.Trim(),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.OperationalTags.Add(tag);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new OperationalTagDto(
            tag.Id, tag.Slug, tag.Name, tag.ColorHex, tag.Category, tag.IsActive, tag.CreatedAtUtc));
    }
}

public record DeactivateOperationalTagForAdminCommand(Guid TagId) : IRequest<Result<OperationalTagDto>>;

public sealed class DeactivateOperationalTagForAdminCommandHandler
    : IRequestHandler<DeactivateOperationalTagForAdminCommand, Result<OperationalTagDto>>
{
    private readonly ITenancyDbContext _dbContext;

    public DeactivateOperationalTagForAdminCommandHandler(ITenancyDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<OperationalTagDto>> Handle(
        DeactivateOperationalTagForAdminCommand request,
        CancellationToken cancellationToken)
    {
        var tag = await _dbContext.OperationalTags
            .FirstOrDefaultAsync(t => t.Id == request.TagId, cancellationToken);

        if (tag is null)
        {
            return Result.Failure<OperationalTagDto>(TenancyErrors.TagNotFound);
        }

        tag.IsActive = false;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new OperationalTagDto(
            tag.Id, tag.Slug, tag.Name, tag.ColorHex, tag.Category, tag.IsActive, tag.CreatedAtUtc));
    }
}

public record GetOperationalTagsQuery(bool IncludeInactive = false) : IRequest<Result<IReadOnlyCollection<OperationalTagDto>>>;

public sealed class GetOperationalTagsQueryHandler
    : IRequestHandler<GetOperationalTagsQuery, Result<IReadOnlyCollection<OperationalTagDto>>>
{
    private readonly ITenancyDbContext _dbContext;

    public GetOperationalTagsQueryHandler(ITenancyDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<IReadOnlyCollection<OperationalTagDto>>> Handle(
        GetOperationalTagsQuery request,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.OperationalTags.AsNoTracking().AsQueryable();
        if (!request.IncludeInactive)
        {
            query = query.Where(t => t.IsActive);
        }

        var tags = await query
            .OrderBy(t => t.Name)
            .Select(t => new OperationalTagDto(t.Id, t.Slug, t.Name, t.ColorHex, t.Category, t.IsActive, t.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyCollection<OperationalTagDto>>(tags);
    }
}
