using System.Text.Json;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;
using CriaCerto.Modules.Backoffice.Application.Features.Tenants.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Backoffice.Application.Features.Tenants.Commands;

public record GetAdminSavedFiltersQuery(Guid AdminUserId) : IRequest<Result<IReadOnlyCollection<AdminSavedFilterDto>>>;

public sealed class GetAdminSavedFiltersQueryHandler
    : IRequestHandler<GetAdminSavedFiltersQuery, Result<IReadOnlyCollection<AdminSavedFilterDto>>>
{
    private readonly DbContext _dbContext;

    public GetAdminSavedFiltersQueryHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<IReadOnlyCollection<AdminSavedFilterDto>>> Handle(
        GetAdminSavedFiltersQuery request,
        CancellationToken cancellationToken)
    {
        var filters = await _dbContext.Set<AdminSavedFilter>()
            .AsNoTracking()
            .Where(f => f.AdminUserId == request.AdminUserId)
            .OrderByDescending(f => f.IsDefault)
            .ThenBy(f => f.Name)
            .ToListAsync(cancellationToken);

        var dtos = filters.Select(ToDto).ToList();
        return Result.Success<IReadOnlyCollection<AdminSavedFilterDto>>(dtos);
    }

    private static AdminSavedFilterDto ToDto(AdminSavedFilter filter)
    {
        var parsed = JsonSerializer.Deserialize<TenantAdminFilterDto>(filter.FilterJson)
            ?? new TenantAdminFilterDto();
        return new AdminSavedFilterDto(filter.Id, filter.Name, parsed, filter.IsDefault, filter.CreatedAtUtc, filter.UpdatedAtUtc);
    }
}

public record SaveAdminFilterCommand(
    Guid AdminUserId,
    string Name,
    TenantAdminFilterDto Filter,
    bool IsDefault
) : IRequest<Result<AdminSavedFilterDto>>;

public sealed class SaveAdminFilterCommandHandler : IRequestHandler<SaveAdminFilterCommand, Result<AdminSavedFilterDto>>
{
    private readonly DbContext _dbContext;

    public SaveAdminFilterCommandHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<AdminSavedFilterDto>> Handle(SaveAdminFilterCommand request, CancellationToken cancellationToken)
    {
        var normalizedName = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return Result.Failure<AdminSavedFilterDto>(BackofficeErrors.InvalidAdminUserData);
        }

        var nameExists = await _dbContext.Set<AdminSavedFilter>()
            .AnyAsync(f => f.AdminUserId == request.AdminUserId && f.Name == normalizedName, cancellationToken);

        if (nameExists)
        {
            return Result.Failure<AdminSavedFilterDto>(BackofficeErrors.SavedFilterNameAlreadyExists);
        }

        if (request.IsDefault)
        {
            var existingDefaults = await _dbContext.Set<AdminSavedFilter>()
                .Where(f => f.AdminUserId == request.AdminUserId && f.IsDefault)
                .ToListAsync(cancellationToken);

            foreach (var existing in existingDefaults)
            {
                existing.ClearDefaultFlag();
            }
        }

        var filterJson = JsonSerializer.Serialize(request.Filter);
        var entity = AdminSavedFilter.Create(request.AdminUserId, normalizedName, filterJson, request.IsDefault);
        _dbContext.Set<AdminSavedFilter>().Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new AdminSavedFilterDto(
            entity.Id,
            entity.Name,
            request.Filter,
            entity.IsDefault,
            entity.CreatedAtUtc,
            entity.UpdatedAtUtc));
    }
}

public record DeleteAdminFilterCommand(Guid AdminUserId, Guid FilterId) : IRequest<Result>;

public sealed class DeleteAdminFilterCommandHandler : IRequestHandler<DeleteAdminFilterCommand, Result>
{
    private readonly DbContext _dbContext;

    public DeleteAdminFilterCommandHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result> Handle(DeleteAdminFilterCommand request, CancellationToken cancellationToken)
    {
        var filter = await _dbContext.Set<AdminSavedFilter>()
            .FirstOrDefaultAsync(f => f.Id == request.FilterId && f.AdminUserId == request.AdminUserId, cancellationToken);

        if (filter is null)
        {
            return Result.Failure(BackofficeErrors.SavedFilterNotFound);
        }

        _dbContext.Set<AdminSavedFilter>().Remove(filter);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
