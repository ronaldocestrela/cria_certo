using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Domain.Errors;

namespace CriaCerto.Modules.Tenancy.Application.Domain;

public sealed class TenantSubscribedModule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string ModuleKey { get; set; } = string.Empty;
    public string? StripeSubscriptionItemId { get; set; }
    public DateTime ActivatedAtUtc { get; set; } = DateTime.UtcNow;
    public decimal MonthlyPriceSnapshot { get; set; }
    public Tenant? Tenant { get; set; }

    public static Result<TenantSubscribedModule> Create(
        Guid tenantId,
        string moduleKey,
        decimal monthlyPriceSnapshot = 0m,
        string? stripeSubscriptionItemId = null,
        DateTime? activatedAtUtc = null)
    {
        if (tenantId == Guid.Empty || monthlyPriceSnapshot < 0m)
        {
            return Result.Failure<TenantSubscribedModule>(TenancyErrors.InvalidSubscribedModuleData);
        }

        var normalizedKey = PlanCapacityLimits.NormalizeCommercialModuleKey(moduleKey);
        if (normalizedKey is null)
        {
            return Result.Failure<TenantSubscribedModule>(TenancyErrors.InvalidModuleKey);
        }

        return Result.Success(new TenantSubscribedModule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ModuleKey = normalizedKey,
            MonthlyPriceSnapshot = monthlyPriceSnapshot,
            StripeSubscriptionItemId = string.IsNullOrWhiteSpace(stripeSubscriptionItemId)
                ? null
                : stripeSubscriptionItemId.Trim(),
            ActivatedAtUtc = activatedAtUtc ?? DateTime.UtcNow
        });
    }
}

