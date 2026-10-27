using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Tenancy.Application.Features.GetUserProfile;

public record GetUserProfileQuery(Guid UserId) : IRequest<Result<UserProfileDto>>;

public sealed record UserTenantAffiliationDto(
    Guid TenantId,
    string TenantName,
    string Role,
    DateTime JoinedAt,
    string SubscribedPlan
);

public sealed record UserProfileDto(
    Guid Id,
    string Email,
    string FullName,
    string? PhoneNumber,
    bool IsEmailConfirmed,
    DateTime? EmailConfirmedAtUtc,
    DateTime CreatedAtUtc,
    DateTime? TermsAcceptedAtUtc,
    DateTime? PrivacyPolicyAcceptedAtUtc,
    string? TermsVersion,
    string? PrivacyPolicyVersion,
    IReadOnlyList<UserTenantAffiliationDto> AssociatedTenants
);

public sealed class GetUserProfileQueryHandler : IRequestHandler<GetUserProfileQuery, Result<UserProfileDto>>
{
    private readonly ITenancyDbContext _dbContext;

    public GetUserProfileQueryHandler(ITenancyDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<UserProfileDto>> Handle(GetUserProfileQuery request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.UserTenants)
            .ThenInclude(ut => ut.Tenant)
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (user == null)
        {
            return Result.Failure<UserProfileDto>(
                Error.NotFound("User.NotFound", "Usuário não encontrado."));
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
