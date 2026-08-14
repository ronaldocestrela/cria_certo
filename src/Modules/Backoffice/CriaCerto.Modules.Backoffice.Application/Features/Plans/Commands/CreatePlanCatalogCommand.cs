using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;
using CriaCerto.Modules.Backoffice.Application.Features.Plans.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Backoffice.Application.Features.Plans.Commands;

public record CreatePlanCatalogCommand(
    string Code,
    string Name,
    string Description,
    string Category,
    decimal InitialMonthlyPrice,
    decimal InitialAnnualPriceMonthly,
    int InitialHeadCapacityLimit,
    List<PlanFeatureInputDto>? InitialFeatures,
    List<PlanLimitInputDto>? InitialLimits,
    Guid PerformedByAdminUserId,
    string PerformedByAdminEmail,
    string IpAddress
) : IRequest<Result<PlanCatalogDto>>;

public sealed class CreatePlanCatalogCommandHandler : IRequestHandler<CreatePlanCatalogCommand, Result<PlanCatalogDto>>
{
    private readonly DbContext _dbContext;

    public CreatePlanCatalogCommandHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<PlanCatalogDto>> Handle(CreatePlanCatalogCommand request, CancellationToken cancellationToken)
    {
        var normalizedCode = request.Code.Trim().ToLowerInvariant();
        var codeExists = await _dbContext.Set<PlanCatalog>()
            .AnyAsync(p => p.Code == normalizedCode, cancellationToken);

        if (codeExists)
        {
            return Result.Failure<PlanCatalogDto>(Error.Conflict(
                "PlanCatalog.CodeAlreadyExists",
                $"Já existe um plano cadastrado com o código '{normalizedCode}'."));
        }

        var planResult = PlanCatalog.Create(request.Code, request.Name, request.Description, request.Category);
        if (planResult.IsFailure) return Result.Failure<PlanCatalogDto>(planResult.Error);

        var plan = planResult.Value;

        var features = request.InitialFeatures?.Select(f => PlanFeature.Create(f.FeatureKey, f.DisplayName, f.IsEnabled, f.FeatureType));
        var limits = request.InitialLimits?.Select(l => PlanLimit.Create(l.LimitKey, l.LimitValue, l.Unit));

        var versionResult = plan.CreateVersion(
            "v1.0 - Inicial",
            request.InitialMonthlyPrice,
            request.InitialAnnualPriceMonthly,
            request.InitialHeadCapacityLimit,
            null,
            null,
            features,
            limits);

        if (versionResult.IsFailure) return Result.Failure<PlanCatalogDto>(versionResult.Error);

        _dbContext.Set<PlanCatalog>().Add(plan);

        var auditLog = AuditLog.Create(
            request.PerformedByAdminUserId,
            request.PerformedByAdminEmail,
            "PlanCatalog.Created",
            $"PlanCatalog/{plan.Id}",
            request.IpAddress,
            System.Text.Json.JsonSerializer.Serialize(new
            {
                plan.Id,
                plan.Code,
                plan.Name,
                plan.Category
            }));

        _dbContext.Set<AuditLog>().Add(auditLog);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(plan.ToDto());
    }
}
