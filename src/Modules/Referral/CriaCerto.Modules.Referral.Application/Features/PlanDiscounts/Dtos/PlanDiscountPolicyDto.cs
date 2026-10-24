using CriaCerto.Modules.Referral.Application.Domain.Enums;

namespace CriaCerto.Modules.Referral.Application.Features.PlanDiscounts.Dtos;

public sealed record PlanDiscountPolicyDto(
    Guid? PolicyId,
    Guid PlanCatalogId,
    string PlanCode,
    string PlanName,
    DiscountType DiscountType,
    decimal DiscountValue,
    decimal MaxDiscountCapPercentage,
    bool IsActive,
    bool HasConfiguredPolicy,
    DateTime? CreatedAtUtc);

public sealed record ConfigurePlanDiscountPolicyRequest(
    DiscountType DiscountType,
    decimal DiscountValue,
    decimal MaxDiscountCapPercentage = 100m);

