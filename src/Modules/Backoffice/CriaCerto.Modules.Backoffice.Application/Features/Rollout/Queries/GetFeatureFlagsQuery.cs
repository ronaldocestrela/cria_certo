using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Features.Rollout.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Backoffice.Application.Features.Rollout.Queries;

public record GetFeatureFlagsQuery : IRequest<Result<IReadOnlyList<FeatureFlagDto>>>;

public class GetFeatureFlagsQueryHandler : IRequestHandler<GetFeatureFlagsQuery, Result<IReadOnlyList<FeatureFlagDto>>>
{
    private readonly DbContext _dbContext;

    public GetFeatureFlagsQueryHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<IReadOnlyList<FeatureFlagDto>>> Handle(GetFeatureFlagsQuery request, CancellationToken cancellationToken)
    {
        var flags = await _dbContext.Set<FeatureFlag>()
            .AsNoTracking()
            .OrderBy(f => f.Category)
            .ThenBy(f => f.Key)
            .ToListAsync(cancellationToken);

        var dtos = flags.Select(flag => new FeatureFlagDto(
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
        )).ToList();

        return Result.Success<IReadOnlyList<FeatureFlagDto>>(dtos);
    }
}
