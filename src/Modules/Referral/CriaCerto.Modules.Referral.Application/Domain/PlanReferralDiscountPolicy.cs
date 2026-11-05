using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Referral.Application.Domain.Enums;
using CriaCerto.Modules.Referral.Application.Domain.Errors;
using CriaCerto.Modules.Tenancy.Application.Domain;

namespace CriaCerto.Modules.Referral.Application.Domain;

public sealed class PlanReferralDiscountPolicy
{
    public const string GlobalDefaultCode = "GLOBAL_DEFAULT";
    public static readonly Guid GlobalDefaultCatalogId = new("11111111-1111-1111-1111-111111111111");

    public Guid Id { get; private set; }
    public Guid PlanCatalogId { get; private set; }
    public string PlanCode { get; private set; } = string.Empty;
    public Guid? HeadTierCatalogId { get; private set; }
    public string? HeadTierCode { get; private set; }
    public DiscountPolicyScope DiscountScope { get; private set; }
    public DiscountType DiscountType { get; private set; }
    public decimal DiscountValue { get; private set; }
    public decimal MaxDiscountCapPercentage { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    private PlanReferralDiscountPolicy() { }

    public static Result<PlanReferralDiscountPolicy> CreateForHeadTier(
        Guid headTierCatalogId,
        string headTierCode,
        DiscountType discountType,
        decimal discountValue,
        decimal maxDiscountCapPercentage = 100m)
    {
        if (headTierCatalogId == Guid.Empty)
            return Result.Failure<PlanReferralDiscountPolicy>(ReferralErrors.Policy.HeadTierCatalogRequired);

        if (!HeadTierCapacityLimits.IsValidHeadTierCode(headTierCode))
            return Result.Failure<PlanReferralDiscountPolicy>(ReferralErrors.Policy.InvalidHeadTierCode);

        var validationResult = ValidateDiscountParameters(discountType, discountValue, maxDiscountCapPercentage);
        if (validationResult.IsFailure)
            return Result.Failure<PlanReferralDiscountPolicy>(validationResult.Error);

        var normalizedTier = HeadTierCapacityLimits.NormalizeHeadTierCode(headTierCode);

        var policy = new PlanReferralDiscountPolicy
        {
            Id = Guid.NewGuid(),
            PlanCatalogId = headTierCatalogId,
            PlanCode = normalizedTier,
            HeadTierCatalogId = headTierCatalogId,
            HeadTierCode = normalizedTier,
            DiscountScope = DiscountPolicyScope.HeadTier,
            DiscountType = discountType,
            DiscountValue = discountValue,
            MaxDiscountCapPercentage = maxDiscountCapPercentage,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        return Result.Success(policy);
    }

    public static Result<PlanReferralDiscountPolicy> CreateGlobalDefault(
        DiscountType discountType,
        decimal discountValue,
        decimal maxDiscountCapPercentage = 100m,
        Guid? customCatalogId = null)
    {
        var validationResult = ValidateDiscountParameters(discountType, discountValue, maxDiscountCapPercentage);
        if (validationResult.IsFailure)
            return Result.Failure<PlanReferralDiscountPolicy>(validationResult.Error);

        var catalogId = customCatalogId.HasValue && customCatalogId.Value != Guid.Empty
            ? customCatalogId.Value
            : GlobalDefaultCatalogId;

        var policy = new PlanReferralDiscountPolicy
        {
            Id = Guid.NewGuid(),
            PlanCatalogId = catalogId,
            PlanCode = GlobalDefaultCode,
            HeadTierCatalogId = null,
            HeadTierCode = null,
            DiscountScope = DiscountPolicyScope.GlobalDefault,
            DiscountType = discountType,
            DiscountValue = discountValue,
            MaxDiscountCapPercentage = maxDiscountCapPercentage,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        return Result.Success(policy);
    }

    public static Result<PlanReferralDiscountPolicy> Create(
        Guid planCatalogId,
        string planCode,
        DiscountType discountType,
        decimal discountValue,
        decimal maxDiscountCapPercentage = 100m)
    {
        if (planCatalogId == Guid.Empty)
            return Result.Failure<PlanReferralDiscountPolicy>(ReferralErrors.Policy.PlanCatalogRequired);

        var validationResult = ValidateDiscountParameters(discountType, discountValue, maxDiscountCapPercentage);
        if (validationResult.IsFailure)
            return Result.Failure<PlanReferralDiscountPolicy>(validationResult.Error);

        var normalizedCode = string.IsNullOrWhiteSpace(planCode) ? string.Empty : planCode.Trim().ToUpperInvariant();

        DiscountPolicyScope scope;
        string? headTierCode = null;
        Guid? headTierCatalogId = null;

        if (normalizedCode.Equals(GlobalDefaultCode, StringComparison.OrdinalIgnoreCase))
        {
            scope = DiscountPolicyScope.GlobalDefault;
        }
        else if (HeadTierCapacityLimits.IsValidHeadTierCode(normalizedCode))
        {
            scope = DiscountPolicyScope.HeadTier;
            headTierCode = HeadTierCapacityLimits.NormalizeHeadTierCode(normalizedCode);
            headTierCatalogId = planCatalogId;
        }
        else
        {
            scope = DiscountPolicyScope.LegacyPlan;
        }

        var policy = new PlanReferralDiscountPolicy
        {
            Id = Guid.NewGuid(),
            PlanCatalogId = planCatalogId,
            PlanCode = normalizedCode,
            HeadTierCatalogId = headTierCatalogId,
            HeadTierCode = headTierCode,
            DiscountScope = scope,
            DiscountType = discountType,
            DiscountValue = discountValue,
            MaxDiscountCapPercentage = maxDiscountCapPercentage,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        return Result.Success(policy);
    }

    private static Result ValidateDiscountParameters(DiscountType discountType, decimal discountValue, decimal maxDiscountCapPercentage)
    {
        if (discountValue <= 0)
            return Result.Failure(ReferralErrors.Policy.InvalidDiscountValue);

        if (discountType == DiscountType.Percentage && discountValue > 100m)
            return Result.Failure(ReferralErrors.Policy.InvalidPercentageDiscountValue);

        if (maxDiscountCapPercentage <= 0m || maxDiscountCapPercentage > 100m)
            return Result.Failure(ReferralErrors.Policy.InvalidDiscountCap);

        return Result.Success();
    }

    public Result UpdatePolicy(
        DiscountType discountType,
        decimal discountValue,
        decimal maxDiscountCapPercentage)
    {
        var validationResult = ValidateDiscountParameters(discountType, discountValue, maxDiscountCapPercentage);
        if (validationResult.IsFailure)
            return validationResult;

        DiscountType = discountType;
        DiscountValue = discountValue;
        MaxDiscountCapPercentage = maxDiscountCapPercentage;
        UpdatedAtUtc = DateTime.UtcNow;

        return Result.Success();
    }

    public decimal CalculateDiscount(decimal basePrice, int activeReferralsCount = 1)
    {
        if (!IsActive || basePrice <= 0 || activeReferralsCount <= 0)
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

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Reactivate()
    {
        IsActive = true;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
