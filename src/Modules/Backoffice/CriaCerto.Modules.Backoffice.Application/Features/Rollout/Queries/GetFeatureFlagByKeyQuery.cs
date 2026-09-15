using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;
using CriaCerto.Modules.Backoffice.Application.Features.Rollout.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Backoffice.Application.Features.Rollout.Queries;

public record GetFeatureFlagByKeyQuery(string FlagKey) : IRequest<Result<FeatureFlagDto>>;

public class GetFeatureFlagByKeyQueryHandler : IRequestHandler<GetFeatureFlagByKeyQuery, Result<FeatureFlagDto>>
{
    private readonly DbContext _dbContext;

    public GetFeatureFlagByKeyQueryHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<FeatureFlagDto>> Handle(GetFeatureFlagByKeyQuery request, CancellationToken cancellationToken)
    {
        var normalizedKey = request.FlagKey.Trim().ToLowerInvariant();
        var flag = await _dbContext.Set<FeatureFlag>()
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Key == normalizedKey, cancellationToken);

        if (flag is null)
        {
            return Result.Failure<FeatureFlagDto>(FeatureFlagErrors.NotFound);
        }

        var dto = new FeatureFlagDto(
            flag.Id,
            flag.Key,
            flag.Name,
            flag.Description,
            flag.Category,
            flag.IsEnabled,
            flag.RolloutPercentage,
            flag.MaxAllowedRing,
            flag.GetWhitelistedEmails(),
            flag.KillSwitchActive,
            flag.KillSwitchReason,
            flag.KillSwitchActivatedAtUtc,
            flag.KillSwitchActivatedBy,
            flag.CreatedAtUtc,
            flag.CreatedBy,
            flag.UpdatedAtUtc,
            flag.UpdatedBy,
            flag.LastToggledReason
        );

        return Result.Success(dto);
    }
}
