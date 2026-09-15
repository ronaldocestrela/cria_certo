using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;
using CriaCerto.Modules.Backoffice.Application.Domain.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Backoffice.Application.Features.Rollout.Queries;

public record EvaluateFeatureFlagQuery(
    string FlagKey,
    string AdminEmail,
    string ActorRole,
    Guid? TenantId = null
) : IRequest<Result<bool>>;

public class EvaluateFeatureFlagQueryHandler : IRequestHandler<EvaluateFeatureFlagQuery, Result<bool>>
{
    private readonly DbContext _dbContext;
    private readonly IFeatureFlagEvaluator _evaluator;

    public EvaluateFeatureFlagQueryHandler(DbContext dbContext, IFeatureFlagEvaluator evaluator)
    {
        _dbContext = dbContext;
        _evaluator = evaluator;
    }

    public async Task<Result<bool>> Handle(EvaluateFeatureFlagQuery request, CancellationToken cancellationToken)
    {
        var normalizedKey = request.FlagKey.Trim().ToLowerInvariant();
        var flag = await _dbContext.Set<FeatureFlag>()
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Key == normalizedKey, cancellationToken);

        if (flag is null)
        {
            return Result.Failure<bool>(FeatureFlagErrors.NotFound);
        }

        var isEnabled = _evaluator.Evaluate(flag, request.AdminEmail, request.ActorRole, request.TenantId);
        return Result.Success(isEnabled);
    }
}
