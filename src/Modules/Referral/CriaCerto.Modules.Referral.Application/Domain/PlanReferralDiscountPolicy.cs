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

        if (discountType == DiscountType.Percentage && discountValue > 100m)
            return Result.Failure<PlanReferralDiscountPolicy>(ReferralErrors.Policy.InvalidPercentageDiscountValue);

        if (maxDiscountCapPercentage <= 0m || maxDiscountCapPercentage > 100m)
            return Result.Failure<PlanReferralDiscountPolicy>(ReferralErrors.Policy.InvalidDiscountCap);

        var policy = new PlanReferralDiscountPolicy
        {
            Id = Guid.NewGuid(),
            PlanCatalogId = planCatalogId,
            PlanCode = string.IsNullOrWhiteSpace(planCode) ? string.Empty : planCode.Trim().ToUpperInvariant(),
            DiscountType = discountType,
            DiscountValue = discountValue,
            MaxDiscountCapPercentage = maxDiscountCapPercentage,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        return Result.Success(policy);
    }

    public Result UpdatePolicy(
        DiscountType discountType,
        decimal discountValue,
        decimal maxDiscountCapPercentage)
    {
        if (discountValue <= 0)
            return Result.Failure(ReferralErrors.Policy.InvalidDiscountValue);

        if (discountType == DiscountType.Percentage && discountValue > 100m)
            return Result.Failure(ReferralErrors.Policy.InvalidPercentageDiscountValue);

        if (maxDiscountCapPercentage <= 0m || maxDiscountCapPercentage > 100m)
            return Result.Failure(ReferralErrors.Policy.InvalidDiscountCap);

        DiscountType = discountType;
        DiscountValue = discountValue;
        MaxDiscountCapPercentage = maxDiscountCapPercentage;

        return Result.Success();
    }

    public decimal CalculateDiscount(decimal basePrice, int activeReferralsCount = 1)
    {
        if (basePrice <= 0 || activeReferralsCount <= 0)
            return 0m;

        decimal calculatedDiscount;
        if (DiscountType == DiscountType.Percentage)
        {
            var rawPercentage = activeReferralsCount * DiscountValue;
            var cappedPercentage = Math.Min(rawPercentage, Math.Min(MaxDiscountCapPercentage, 100m));
            calculatedDiscount = basePrice * (cappedPercentage / 100m);
        }
        else
        {
            calculatedDiscount = activeReferralsCount * DiscountValue;
        }

        // Teto inegociável: máximo de 100% da assinatura do período (nunca saldo negativo ou dinheiro de volta)
        var maxAllowedCapPercentage = Math.Min(MaxDiscountCapPercentage, 100m);
        var maxAllowedDiscount = basePrice * (maxAllowedCapPercentage / 100m);

        return Math.Min(calculatedDiscount, maxAllowedDiscount);
    }

    public void Deactivate() => IsActive = false;
    public void Reactivate() => IsActive = true;
}
