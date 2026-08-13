using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;
using CriaCerto.Modules.Backoffice.Application.Features.AdminUsers.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Backoffice.Application.Features.AdminUsers.Commands;

public record UpdateAdminUserCommand(
    Guid Id,
    string Name,
    string Email,
    List<Guid> RoleIds,
    Guid PerformedByAdminUserId,
    string PerformedByAdminEmail,
    string IpAddress
) : IRequest<Result<AdminUserSummaryDto>>;

public class UpdateAdminUserCommandHandler : IRequestHandler<UpdateAdminUserCommand, Result<AdminUserSummaryDto>>
{
    private readonly DbContext _dbContext;

    public UpdateAdminUserCommandHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<AdminUserSummaryDto>> Handle(UpdateAdminUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Set<AdminUser>()
            .Include(u => u.Roles)
                .ThenInclude(r => r.Permissions)
            .FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken);

        if (user is null)
        {
            return Result.Failure<AdminUserSummaryDto>(BackofficeErrors.UserNotFound);
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        bool emailInUseByAnother = await _dbContext.Set<AdminUser>()
            .AnyAsync(u => u.Email == normalizedEmail && u.Id != user.Id, cancellationToken);

        if (emailInUseByAnother)
        {
            return Result.Failure<AdminUserSummaryDto>(Error.Conflict(
                "Backoffice.EmailAlreadyInUse",
                "O e-mail informado já está em uso por outro usuário administrativo."));
        }

        var updateResult = user.UpdateDetails(request.Name, request.Email);
        if (updateResult.IsFailure)
        {
            return Result.Failure<AdminUserSummaryDto>(updateResult.Error);
        }

        // Sync roles
        var existingRoleIds = user.Roles.Select(r => r.Id).ToList();
        var rolesToRemove = existingRoleIds.Except(request.RoleIds).ToList();
        foreach (var roleId in rolesToRemove)
        {
            user.RemoveRole(roleId);
        }

        var rolesToAdd = request.RoleIds.Except(existingRoleIds).ToList();
        if (rolesToAdd.Any())
        {
            var newRoles = await _dbContext.Set<AdminRole>()
                .Include(r => r.Permissions)
                .Where(r => rolesToAdd.Contains(r.Id))
                .ToListAsync(cancellationToken);

            foreach (var role in newRoles)
            {
                user.AssignRole(role);
            }
        }

        var auditLog = AuditLog.Create(
            request.PerformedByAdminUserId,
            request.PerformedByAdminEmail,
            "AdminUser.Updated",
            $"AdminUser/{user.Id}",
            request.IpAddress,
            System.Text.Json.JsonSerializer.Serialize(new { user.Id, user.Email, user.Name, request.RoleIds }));

        _dbContext.Set<AuditLog>().Add(auditLog);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var summaryDto = new AdminUserSummaryDto(
            user.Id,
            user.Name,
            user.Email,
            user.IsActive,
            user.MfaEnabled,
            user.RequiresMfa(),
            user.MustChangePasswordOnNextLogin,
            user.CreatedAtUtc,
            user.LastLoginAtUtc,
            user.Roles.Select(r => r.Name).ToList()
        );

        return Result.Success(summaryDto);
    }
}
