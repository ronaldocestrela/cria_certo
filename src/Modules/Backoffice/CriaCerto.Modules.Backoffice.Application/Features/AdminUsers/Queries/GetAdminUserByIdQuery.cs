using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;
using CriaCerto.Modules.Backoffice.Application.Features.AdminUsers.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Backoffice.Application.Features.AdminUsers.Queries;

public record GetAdminUserByIdQuery(Guid Id) : IRequest<Result<AdminUserDetailDto>>;

public class GetAdminUserByIdQueryHandler : IRequestHandler<GetAdminUserByIdQuery, Result<AdminUserDetailDto>>
{
    private readonly DbContext _dbContext;

    public GetAdminUserByIdQueryHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<AdminUserDetailDto>> Handle(GetAdminUserByIdQuery request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Set<AdminUser>()
            .Include(u => u.Roles)
                .ThenInclude(r => r.Permissions)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken);

        if (user is null)
        {
            return Result.Failure<AdminUserDetailDto>(BackofficeErrors.UserNotFound);
        }

        var now = DateTime.UtcNow;
        var activeSessions = await _dbContext.Set<AdminSession>()
            .AsNoTracking()
            .Where(s => s.AdminUserId == user.Id && !s.IsRevoked && s.RefreshTokenExpiresAtUtc > now)
            .OrderByDescending(s => s.CreatedAtUtc)
            .Select(s => new AdminSessionDto(
                s.Id,
                s.AdminUserId,
                s.SessionToken,
                s.IpAddress,
                s.UserAgent,
                s.CreatedAtUtc,
                s.ExpiresAtUtc,
                s.RefreshTokenExpiresAtUtc,
                s.IsRevoked,
                !s.IsRevoked && s.RefreshTokenExpiresAtUtc > now
            ))
            .ToListAsync(cancellationToken);

        var roleDtos = user.Roles.Select(r => new AdminRoleSummaryDto(
            r.Id,
            r.Name,
            r.Description,
            r.Permissions.Select(p => p.Name).ToList()
        )).ToList();

        var detailDto = new AdminUserDetailDto(
            user.Id,
            user.Name,
            user.Email,
            user.IsActive,
            user.MfaEnabled,
            user.RequiresMfa(),
            user.MustChangePasswordOnNextLogin,
            user.CreatedAtUtc,
            user.LastLoginAtUtc,
            roleDtos,
            activeSessions
        );

        return Result.Success(detailDto);
    }
}
