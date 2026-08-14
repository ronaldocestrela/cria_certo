using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;
using CriaCerto.Modules.Backoffice.Application.Features.Plans.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Backoffice.Application.Features.Plans.Commands;

public record CreatePlanVersionCommand(
    Guid PlanCatalogId,
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

public sealed class CreatePlanVersionCommandHandler : IRequestHandler<CreatePlanVersionCommand, Result<PlanVersionDto>>
{
    private readonly DbContext _dbContext;

    public CreatePlanVersionCommandHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<PlanVersionDto>> Handle(CreatePlanVersionCommand request, CancellationToken cancellationToken)
    {
        var plan = await _dbContext.Set<PlanCatalog>()
            .Include(p => p.Versions)
                .ThenInclude(v => v.Features)
            .Include(p => p.Versions)
                .ThenInclude(v => v.Limits)
            .FirstOrDefaultAsync(p => p.Id == request.PlanCatalogId, cancellationToken);

        if (plan is null)
        {
            return Result.Failure<PlanVersionDto>(PlanErrors.PlanNotFound);
        }

        var features = request.Features?.Select(f => PlanFeature.Create(f.FeatureKey, f.DisplayName, f.IsEnabled, f.FeatureType));
        var limits = request.Limits?.Select(l => PlanLimit.Create(l.LimitKey, l.LimitValue, l.Unit));

        var versionResult = plan.CreateVersion(
            request.VersionName,
            request.MonthlyPrice,
            request.AnnualPriceMonthly,
            request.HeadCapacityLimit,
            request.MaxUsers,
            request.MaxProductionUnits,
            features,
            limits);

        if (versionResult.IsFailure) return Result.Failure<PlanVersionDto>(versionResult.Error);

        var version = versionResult.Value;
        _dbContext.Set<PlanVersion>().Add(version);

        var auditLog = AuditLog.Create(
            request.PerformedByAdminUserId,
            request.PerformedByAdminEmail,
            "PlanVersion.Created",
            $"PlanVersion/{version.Id}",
            request.IpAddress,
            System.Text.Json.JsonSerializer.Serialize(new
            {
                version.Id,
                version.PlanCatalogId,
                version.VersionNumber,
                version.VersionName,
                version.MonthlyPrice
            }));

        _dbContext.Set<AuditLog>().Add(auditLog);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(version.ToDto());
    }
}
