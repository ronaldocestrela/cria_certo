using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;

namespace CriaCerto.Modules.Backoffice.Application.Domain.Entities;

public class ModuleTierPrice
{
    public Guid Id { get; private set; }
    public Guid ModuleCatalogId { get; private set; }
    public Guid HeadTierCatalogId { get; private set; }
    public int VersionNumber { get; private set; }
    public PlanVersionStatus Status { get; private set; } = PlanVersionStatus.Draft;
    public decimal MonthlyPrice { get; private set; }
    public decimal AnnualPriceMonthly { get; private set; }
    public string? StripePriceIdMonthly { get; private set; }
    public string? StripePriceIdAnnual { get; private set; }
    public DateTimeOffset? PublishedAtUtc { get; private set; }
    public Guid? PublishedByAdminId { get; private set; }
    public string? ApprovalNotes { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; } = DateTimeOffset.UtcNow;

    private ModuleTierPrice() { }

    public static Result<ModuleTierPrice> CreateDraft(
        Guid moduleCatalogId,
        Guid headTierCatalogId,
        int versionNumber,
        decimal monthlyPrice,
        decimal annualPriceMonthly,
        string? stripePriceIdMonthly = null,
        string? stripePriceIdAnnual = null)
    {
        if (moduleCatalogId == Guid.Empty ||
            headTierCatalogId == Guid.Empty ||
            versionNumber <= 0 ||
            monthlyPrice < 0 ||
            annualPriceMonthly < 0)
        {
            return Result.Failure<ModuleTierPrice>(PlanErrors.InvalidModuleTierPriceData);
        }

        return Result.Success(new ModuleTierPrice
        {
            Id = Guid.NewGuid(),
            ModuleCatalogId = moduleCatalogId,
            HeadTierCatalogId = headTierCatalogId,
            VersionNumber = versionNumber,
            Status = PlanVersionStatus.Draft,
            MonthlyPrice = monthlyPrice,
            AnnualPriceMonthly = annualPriceMonthly,
            StripePriceIdMonthly = string.IsNullOrWhiteSpace(stripePriceIdMonthly) ? null : stripePriceIdMonthly.Trim(),
            StripePriceIdAnnual = string.IsNullOrWhiteSpace(stripePriceIdAnnual) ? null : stripePriceIdAnnual.Trim(),
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
    }

    public Result UpdateDraft(
        decimal monthlyPrice,
        decimal annualPriceMonthly,
        string? stripePriceIdMonthly = null,
        string? stripePriceIdAnnual = null)
    {
        if (Status != PlanVersionStatus.Draft)
        {
            return Result.Failure(PlanErrors.PublishedVersionImmutable);
        }

        if (monthlyPrice < 0 || annualPriceMonthly < 0)
        {
            return Result.Failure(PlanErrors.InvalidModuleTierPriceData);
        }

        MonthlyPrice = monthlyPrice;
        AnnualPriceMonthly = annualPriceMonthly;
        StripePriceIdMonthly = string.IsNullOrWhiteSpace(stripePriceIdMonthly) ? null : stripePriceIdMonthly.Trim();
        StripePriceIdAnnual = string.IsNullOrWhiteSpace(stripePriceIdAnnual) ? null : stripePriceIdAnnual.Trim();

        return Result.Success();
    }

    public Result Publish(Guid adminUserId, string? approvalNotes = null)
    {
        if (Status != PlanVersionStatus.Draft)
        {
            return Result.Failure(PlanErrors.VersionNotDraft);
        }

        Status = PlanVersionStatus.Published;
        PublishedAtUtc = DateTimeOffset.UtcNow;
        PublishedByAdminId = adminUserId;
        ApprovalNotes = approvalNotes?.Trim();

        return Result.Success();
    }

    public Result Deprecate()
    {
        if (Status != PlanVersionStatus.Published)
        {
            return Result.Failure(PlanErrors.CannotDeprecateNonPublished);
        }

        Status = PlanVersionStatus.Deprecated;
        return Result.Success();
    }
}

