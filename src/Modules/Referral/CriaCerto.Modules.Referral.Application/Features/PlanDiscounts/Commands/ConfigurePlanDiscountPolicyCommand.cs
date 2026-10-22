using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Abstractions;
using CriaCerto.Modules.Referral.Application.Domain;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Domain.Errors;
using CriaCerto.Modules.Referral.Application.Features.PlanDiscounts.Dtos;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Referral.Application.Features.PlanDiscounts.Commands;

public sealed record ConfigurePlanDiscountPolicyCommand(
    Guid PlanCatalogId,
    DiscountType DiscountType,
    decimal DiscountValue,
    decimal MaxDiscountCapPercentage = 100m) : IRequest<Result<PlanDiscountPolicyDto>>;

public sealed class ConfigurePlanDiscountPolicyCommandValidator : AbstractValidator<ConfigurePlanDiscountPolicyCommand>
{
    public ConfigurePlanDiscountPolicyCommandValidator()
    {
        RuleFor(x => x.PlanCatalogId)
            .NotEmpty()
            .WithMessage("O identificador do plano é obrigatório.");

        RuleFor(x => x.DiscountValue)
            .GreaterThan(0)
            .WithMessage("O valor ou percentual de desconto deve ser maior que zero.");

        When(x => x.DiscountType == DiscountType.Percentage, () =>
        {
            RuleFor(x => x.DiscountValue)
                .LessThanOrEqualTo(100)
                .WithMessage("O desconto percentual não pode exceder 100%.");
        });

        RuleFor(x => x.MaxDiscountCapPercentage)
            .InclusiveBetween(1, 100)
            .WithMessage("O teto máximo de desconto deve estar entre 1% e 100%.");
    }
}

public sealed class ConfigurePlanDiscountPolicyCommandHandler
    : IRequestHandler<ConfigurePlanDiscountPolicyCommand, Result<PlanDiscountPolicyDto>>
{
    private readonly IReferralDbContext _dbContext;
    private readonly IPlanCatalogLookupService _planCatalogLookup;

    public ConfigurePlanDiscountPolicyCommandHandler(
        IReferralDbContext dbContext,
        IPlanCatalogLookupService planCatalogLookup)
    {
        _dbContext = dbContext;
        _planCatalogLookup = planCatalogLookup;
    }

    public async Task<Result<PlanDiscountPolicyDto>> Handle(
        ConfigurePlanDiscountPolicyCommand request,
        CancellationToken cancellationToken)
    {
        if (request.PlanCatalogId == Guid.Empty)
        {
            return Result.Failure<PlanDiscountPolicyDto>(ReferralErrors.Policy.PlanCatalogRequired);
        }

        var plans = await _planCatalogLookup.GetAvailablePlansAsync(cancellationToken);
        var plan = plans.FirstOrDefault(p => p.Id == request.PlanCatalogId);
        if (plan is null)
        {
            return Result.Failure<PlanDiscountPolicyDto>(ReferralErrors.Policy.PlanCatalogRequired);
        }

        var policy = await _dbContext.PlanDiscountPolicies
            .FirstOrDefaultAsync(p => p.PlanCatalogId == request.PlanCatalogId, cancellationToken);

        if (policy is null)
        {
            var createResult = PlanReferralDiscountPolicy.Create(
                plan.Id,
                plan.Code,
                request.DiscountType,
                request.DiscountValue,
                request.MaxDiscountCapPercentage);

            if (createResult.IsFailure)
            {
                return Result.Failure<PlanDiscountPolicyDto>(createResult.Error);
            }

            policy = createResult.Value;
            _dbContext.PlanDiscountPolicies.Add(policy);
        }
        else
        {
            var updateResult = policy.UpdatePolicy(
                request.DiscountType,
                request.DiscountValue,
                request.MaxDiscountCapPercentage);

            if (updateResult.IsFailure)
            {
                return Result.Failure<PlanDiscountPolicyDto>(updateResult.Error);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var dto = new PlanDiscountPolicyDto(
            policy.Id,
            policy.PlanCatalogId,
            policy.PlanCode,
            plan.Name,
            policy.DiscountType,
            policy.DiscountValue,
            policy.MaxDiscountCapPercentage,
            policy.IsActive,
            HasConfiguredPolicy: true,
            policy.CreatedAtUtc);

        return Result.Success(dto);
    }
}
