using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Domain.Errors;

namespace CriaCerto.Modules.Referral.Application.Domain;

public sealed class PlanReferralDiscountPolicy
{
    public Guid Id { get; private set; }
    public Guid PlanCatalogId { get; private set; }
    public string PlanCode { get; private set; } = string.Empty;
    public DiscountType DiscountType { get; private set; }
    public decimal DiscountValue { get; private set; }
    public decimal MaxDiscountCapPercentage { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private PlanReferralDiscountPolicy() { }

    public static Result<PlanReferralDiscountPolicy> Create(
        Guid planCatalogId,
        string planCode,
        DiscountType discountType,
        decimal discountValue,
        decimal maxDiscountCapPercentage = 100m)
    {
        if (planCatalogId == Guid.Empty)
            return Result.Failure<PlanReferralDiscountPolicy>(ReferralErrors.Policy.PlanCatalogRequired);

        if (discountValue <= 0)
            return Result.Failure<PlanReferralDiscountPolicy>(ReferralErrors.Policy.InvalidDiscountValue);

        var policy = new PlanReferralDiscountPolicy
        {
            Id = Guid.NewGuid(),
            PlanCatalogId = planCatalogId,
            PlanCode = string.IsNullOrWhiteSpace(planCode) ? string.Empty : planCode.Trim().ToUpperInvariant(),
            DiscountType = discountType,
            DiscountValue = discountValue,
            MaxDiscountCapPercentage = maxDiscountCapPercentage <= 0 ? 100m : maxDiscountCapPercentage,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        return Result.Success(policy);
    }

    public decimal CalculateDiscount(decimal basePrice)
    {
        if (basePrice <= 0)
            return 0m;

        decimal calculatedDiscount;
        if (DiscountType == DiscountType.Percentage)
        {
            calculatedDiscount = basePrice * (DiscountValue / 100m);
        }
        else
        {
            calculatedDiscount = DiscountValue;
        }

        var maxAllowedDiscount = basePrice * (MaxDiscountCapPercentage / 100m);
        return Math.Min(calculatedDiscount, maxAllowedDiscount);
    }

    public void Deactivate() => IsActive = false;
    public void Reactivate() => IsActive = true;
}
