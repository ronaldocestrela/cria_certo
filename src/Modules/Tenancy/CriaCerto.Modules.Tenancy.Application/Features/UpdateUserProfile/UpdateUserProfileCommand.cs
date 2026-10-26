using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Features.GetUserProfile;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Tenancy.Application.Features.UpdateUserProfile;

public record UpdateUserProfileRequest(string FullName, string? PhoneNumber);

public record UpdateUserProfileCommand(
    Guid UserId,
    string FullName,
    string? PhoneNumber,
    string? ClientIpAddress = null
) : IRequest<Result<UserProfileDto>>;

public sealed class UpdateUserProfileCommandHandler : IRequestHandler<UpdateUserProfileCommand, Result<UserProfileDto>>
{
    private readonly ITenancyDbContext _dbContext;
    private readonly ITenancyAuditSink? _auditSink;

    public UpdateUserProfileCommandHandler(
        ITenancyDbContext dbContext,
        ITenancyAuditSink? auditSink = null)
    {
        _dbContext = dbContext;
        _auditSink = auditSink;
    }

    public async Task<Result<UserProfileDto>> Handle(UpdateUserProfileCommand request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .Include(u => u.UserTenants)
            .ThenInclude(ut => ut.Tenant)
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (user == null)
        {
            return Result.Failure<UserProfileDto>(
                Error.NotFound("User.NotFound", "Usuário não encontrado."));
        }

        var oldFullName = user.FullName;
        var oldPhoneNumber = user.PhoneNumber;

        var updateResult = user.UpdateProfile(request.FullName, request.PhoneNumber);
        if (updateResult.IsFailure)
        {
            return Result.Failure<UserProfileDto>(updateResult.Error);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (_auditSink != null)
        {
            await _auditSink.RecordUserProfileUpdateAuditAsync(
                user.Id,
                user.Email,
                oldFullName,
                user.FullName,
                oldPhoneNumber,
                user.PhoneNumber,
                request.ClientIpAddress ?? "127.0.0.1",
                cancellationToken);
        }

        var associatedTenants = user.UserTenants
            .Where(ut => ut.Tenant != null)
            .Select(ut => new UserTenantAffiliationDto(
                ut.TenantId,
                ut.Tenant!.Name,
                ut.Role.ToString(),
                ut.JoinedAt,
                ut.Tenant.SubscribedPlan))
            .ToList();

        var dto = new UserProfileDto(
            user.Id,
            user.Email,
            user.FullName,
            user.PhoneNumber,
            user.IsEmailConfirmed,
            user.EmailConfirmedAtUtc,
            user.CreatedAtUtc,
            user.TermsAcceptedAtUtc,
            user.PrivacyPolicyAcceptedAtUtc,
            user.TermsVersion,
            user.PrivacyPolicyVersion,
            associatedTenants
        );

        return Result.Success(dto);
    }
}
