using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Features.AdminUsers.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Backoffice.Application.Features.AdminUsers.Queries;

public record GetAdminUsersQuery(
    string? SearchTerm = null,
    bool? IsActive = null,
    string? RoleName = null,
    int Page = 1,
    int PageSize = 20
) : IRequest<Result<PagedResult<AdminUserSummaryDto>>>;

public record PagedResult<T>(
    IReadOnlyCollection<T> Items,
    int TotalCount,
    int Page,
    int PageSize
);

public class GetAdminUsersQueryHandler : IRequestHandler<GetAdminUsersQuery, Result<PagedResult<AdminUserSummaryDto>>>
{
    private readonly DbContext _dbContext;

    public GetAdminUsersQueryHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<PagedResult<AdminUserSummaryDto>>> Handle(GetAdminUsersQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.Set<AdminUser>()
            .Include(u => u.Roles)
                .ThenInclude(r => r.Permissions)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(u => u.Name.ToLower().Contains(term) || u.Email.ToLower().Contains(term));
        }

        if (request.IsActive.HasValue)
        {
            query = query.Where(u => u.IsActive == request.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.RoleName))
        {
            var roleName = request.RoleName.Trim();
            query = query.Where(u => u.Roles.Any(r => r.Name == roleName));
        }

        int totalCount = await query.CountAsync(cancellationToken);

        var users = await query
            .OrderByDescending(u => u.CreatedAtUtc)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var items = users.Select(u => new AdminUserSummaryDto(
            u.Id,
            u.Name,
            u.Email,
            u.IsActive,
            u.MfaEnabled,
            u.RequiresMfa(),
            u.MustChangePasswordOnNextLogin,
            u.CreatedAtUtc,
            u.LastLoginAtUtc,
            u.Roles.Select(r => r.Name).ToList()
        )).ToList();

        return Result.Success(new PagedResult<AdminUserSummaryDto>(items, totalCount, request.Page, request.PageSize));
    }
}
