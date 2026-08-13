using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;
using CriaCerto.Modules.Backoffice.Application.Features.AdminUsers.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Backoffice.Application.Features.AdminUsers.Commands;

public record CreateAdminUserCommand(
    string Name,
    string Email,
    string RawPassword,
    List<Guid> RoleIds,
    Guid PerformedByAdminUserId,
    string PerformedByAdminEmail,
    string IpAddress
) : IRequest<Result<AdminUserSummaryDto>>;

public class CreateAdminUserCommandHandler : IRequestHandler<CreateAdminUserCommand, Result<AdminUserSummaryDto>>
{
    private readonly DbContext _dbContext;

    public CreateAdminUserCommandHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<AdminUserSummaryDto>> Handle(CreateAdminUserCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RawPassword) || request.RawPassword.Length < 6)
        {
            return Result.Failure<AdminUserSummaryDto>(BackofficeErrors.WeakPassword);
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        bool exists = await _dbContext.Set<AdminUser>()
            .AnyAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (exists)
        {
            return Result.Failure<AdminUserSummaryDto>(Error.Conflict(
                "Backoffice.EmailAlreadyInUse",
                "O e-mail informado já está em uso por outro usuário administrativo."));
        }

        // Standard simple hash representation for application handler abstraction
        string passwordHash = $"hash_{request.RawPassword}";

        var createResult = AdminUser.Create(
            request.Name,
            request.Email,
            passwordHash,
            mustChangePasswordOnNextLogin: true);

        if (createResult.IsFailure)
        {
            return Result.Failure<AdminUserSummaryDto>(createResult.Error);
        }

        var user = createResult.Value;

        if (request.RoleIds != null && request.RoleIds.Any())
        {
            var roles = await _dbContext.Set<AdminRole>()
                .Include(r => r.Permissions)
                .Where(r => request.RoleIds.Contains(r.Id))
                .ToListAsync(cancellationToken);

            foreach (var role in roles)
            {
                user.AssignRole(role);
            }
        }

        _dbContext.Set<AdminUser>().Add(user);

        var auditLog = AuditLog.Create(
            request.PerformedByAdminUserId,
            request.PerformedByAdminEmail,
            "AdminUser.Created",
            $"AdminUser/{user.Id}",
            request.IpAddress,
            System.Text.Json.JsonSerializer.Serialize(new { user.Id, user.Email, user.Name }));

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
