using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;
using CriaCerto.Modules.Backoffice.Application.Features.Plans.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Backoffice.Application.Features.Plans.Commands;

public record UpdateDraftPlanVersionCommand(
    Guid VersionId,
    string VersionName,
    decimal MonthlyPrice,
    decimal AnnualPriceMonthly,
    int HeadCapacityLimit,
    int? MaxUsers,
    int? MaxProductionUnits,
    List<PlanFeatureInputDto>? Features,
    List<PlanLimitInputDto>? Limits,
    Guid PerformedByAdminUserId,
    string PerformedByAdminEmail,
    string IpAddress
) : IRequest<Result<PlanVersionDto>>;

public sealed class UpdateDraftPlanVersionCommandHandler : IRequestHandler<UpdateDraftPlanVersionCommand, Result<PlanVersionDto>>
{
    private readonly DbContext _dbContext;

    public UpdateDraftPlanVersionCommandHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<PlanVersionDto>> Handle(UpdateDraftPlanVersionCommand request, CancellationToken cancellationToken)
    {
        var version = await _dbContext.Set<PlanVersion>()
            .Include(v => v.Features)
            .Include(v => v.Limits)
            .FirstOrDefaultAsync(v => v.Id == request.VersionId, cancellationToken);

        if (version is null)
        {
            return Result.Failure<PlanVersionDto>(PlanErrors.VersionNotFound);
        }

        var updateResult = version.UpdateDraft(
            request.VersionName,
            request.MonthlyPrice,
            request.AnnualPriceMonthly,
            request.HeadCapacityLimit,
            request.MaxUsers,
            request.MaxProductionUnits);

        if (updateResult.IsFailure) return Result.Failure<PlanVersionDto>(updateResult.Error);

        if (version.Features.Any())
        {
            _dbContext.Set<PlanFeature>().RemoveRange(version.Features);
        }
        if (version.Limits.Any())
        {
            _dbContext.Set<PlanLimit>().RemoveRange(version.Limits);
        }

        if (request.Features != null)
        {
            foreach (var f in request.Features)
            {
                var feature = PlanFeature.Create(f.FeatureKey, f.DisplayName, f.IsEnabled, f.FeatureType);
                feature.SetPlanVersionId(version.Id);
                _dbContext.Set<PlanFeature>().Add(feature);
            }
        }

        if (request.Limits != null)
        {
            foreach (var l in request.Limits)
            {
                var limit = PlanLimit.Create(l.LimitKey, l.LimitValue, l.Unit);
                limit.SetPlanVersionId(version.Id);
                _dbContext.Set<PlanLimit>().Add(limit);
            }
        }

        var auditLog = AuditLog.Create(
            request.PerformedByAdminUserId,
            request.PerformedByAdminEmail,
            "PlanVersion.UpdatedDraft",
            $"PlanVersion/{version.Id}",
            request.IpAddress,
            System.Text.Json.JsonSerializer.Serialize(new
            {
                version.Id,
                version.VersionName,
                version.MonthlyPrice
            }));

        _dbContext.Set<AuditLog>().Add(auditLog);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var updatedVersion = await _dbContext.Set<PlanVersion>()
            .Include(v => v.Features)
            .Include(v => v.Limits)
            .FirstAsync(v => v.Id == version.Id, cancellationToken);

        return Result.Success(updatedVersion.ToDto());
    }
}
