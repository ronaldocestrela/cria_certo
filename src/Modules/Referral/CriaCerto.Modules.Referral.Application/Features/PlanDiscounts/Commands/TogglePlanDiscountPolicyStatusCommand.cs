using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Abstractions;
using CriaCerto.Modules.Referral.Application.Domain.Errors;
using CriaCerto.Modules.Referral.Application.Features.PlanDiscounts.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Referral.Application.Features.PlanDiscounts.Commands;

public sealed record TogglePlanDiscountPolicyStatusCommand(Guid PolicyId) : IRequest<Result<PlanDiscountPolicyDto>>;

public sealed class TogglePlanDiscountPolicyStatusCommandHandler
    : IRequestHandler<TogglePlanDiscountPolicyStatusCommand, Result<PlanDiscountPolicyDto>>
{
    private readonly IReferralDbContext _dbContext;
    private readonly IPlanCatalogLookupService _planCatalogLookup;

    public TogglePlanDiscountPolicyStatusCommandHandler(
        IReferralDbContext dbContext,
        IPlanCatalogLookupService planCatalogLookup)
    {
        _dbContext = dbContext;
        _planCatalogLookup = planCatalogLookup;
    }

    public async Task<Result<PlanDiscountPolicyDto>> Handle(
        TogglePlanDiscountPolicyStatusCommand request,
        CancellationToken cancellationToken)
    {
        var policy = await _dbContext.PlanDiscountPolicies
            .FirstOrDefaultAsync(p => p.Id == request.PolicyId, cancellationToken);

        if (policy is null)
        {
            return Result.Failure<PlanDiscountPolicyDto>(ReferralErrors.Policy.NotFound);
        }

        if (policy.IsActive)
        {
            policy.Deactivate();
        }
        else
        {
            policy.Reactivate();
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var plans = await _planCatalogLookup.GetAvailablePlansAsync(cancellationToken);
        var planName = plans.FirstOrDefault(p => p.Id == policy.PlanCatalogId)?.Name ?? policy.PlanCode;

        var dto = new PlanDiscountPolicyDto(
            policy.Id,
            policy.PlanCatalogId,
            policy.PlanCode,
            planName,
            policy.DiscountType,
            policy.DiscountValue,
            policy.MaxDiscountCapPercentage,
            policy.IsActive,
            HasConfiguredPolicy: true,
            policy.CreatedAtUtc);

        return Result.Success(dto);
    }
}
