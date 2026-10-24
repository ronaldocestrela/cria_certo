using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Abstractions;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Features.PlanDiscounts.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Referral.Application.Features.PlanDiscounts.Queries;

public sealed record GetPlanDiscountPoliciesQuery : IRequest<Result<IReadOnlyList<PlanDiscountPolicyDto>>>;

public sealed class GetPlanDiscountPoliciesQueryHandler
    : IRequestHandler<GetPlanDiscountPoliciesQuery, Result<IReadOnlyList<PlanDiscountPolicyDto>>>
{
    private readonly IReferralDbContext _dbContext;
    private readonly IPlanCatalogLookupService _planCatalogLookup;

    public GetPlanDiscountPoliciesQueryHandler(
        IReferralDbContext dbContext,
        IPlanCatalogLookupService planCatalogLookup)
    {
        _dbContext = dbContext;
        _planCatalogLookup = planCatalogLookup;
    }

    public async Task<Result<IReadOnlyList<PlanDiscountPolicyDto>>> Handle(
        GetPlanDiscountPoliciesQuery request,
        CancellationToken cancellationToken)
    {
        var plans = await _planCatalogLookup.GetAvailablePlansAsync(cancellationToken);
        var policies = await _dbContext.PlanDiscountPolicies
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var result = new List<PlanDiscountPolicyDto>();

        foreach (var plan in plans)
        {
            var policy = policies.FirstOrDefault(p => p.PlanCatalogId == plan.Id);

            if (policy != null)
            {
                result.Add(new PlanDiscountPolicyDto(
                    policy.Id,
                    policy.PlanCatalogId,
                    policy.PlanCode,
                    plan.Name,
                    policy.DiscountType,
                    policy.DiscountValue,
                    policy.MaxDiscountCapPercentage,
                    policy.IsActive,
                    HasConfiguredPolicy: true,
                    policy.CreatedAtUtc));
            }
            else
            {
                result.Add(new PlanDiscountPolicyDto(
                    null,
                    plan.Id,
                    plan.Code,
                    plan.Name,
                    DiscountType.FixedValue,
                    DiscountValue: 0m,
                    MaxDiscountCapPercentage: 100m,
                    IsActive: false,
                    HasConfiguredPolicy: false,
                    CreatedAtUtc: null));
            }
        }

        // Include any orphaned policies if existing in DB but not in returned active plans
        foreach (var policy in policies.Where(p => plans.All(pl => pl.Id != p.PlanCatalogId)))
        {
            result.Add(new PlanDiscountPolicyDto(
                policy.Id,
                policy.PlanCatalogId,
                policy.PlanCode,
                policy.PlanCode,
                policy.DiscountType,
                policy.DiscountValue,
                policy.MaxDiscountCapPercentage,
                policy.IsActive,
                HasConfiguredPolicy: true,
                policy.CreatedAtUtc));
        }

        return Result.Success<IReadOnlyList<PlanDiscountPolicyDto>>(result.OrderBy(p => p.PlanCode).ToList());
    }
}
