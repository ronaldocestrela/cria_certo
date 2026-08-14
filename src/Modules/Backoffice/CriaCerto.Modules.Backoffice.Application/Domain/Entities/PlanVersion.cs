using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;

namespace CriaCerto.Modules.Backoffice.Application.Domain.Entities;

public class PlanVersion
{
    private readonly List<PlanFeature> _features = new();
    private readonly List<PlanLimit> _limits = new();

    public Guid Id { get; private set; }
    public Guid PlanCatalogId { get; private set; }
    public int VersionNumber { get; private set; }
    public string VersionName { get; private set; } = default!;
    public PlanVersionStatus Status { get; private set; } = PlanVersionStatus.Draft;
    public decimal MonthlyPrice { get; private set; }
    public decimal AnnualPriceMonthly { get; private set; }
    public int HeadCapacityLimit { get; private set; }
    public int? MaxUsers { get; private set; }
    public int? MaxProductionUnits { get; private set; }
    public DateTimeOffset? EffectiveFrom { get; private set; }
    public DateTimeOffset? EffectiveTo { get; private set; }
    public DateTimeOffset? PublishedAtUtc { get; private set; }
    public Guid? PublishedByAdminId { get; private set; }
    public string? ApprovalNotes { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; } = DateTimeOffset.UtcNow;

    public IReadOnlyCollection<PlanFeature> Features => _features.AsReadOnly();
    public IReadOnlyCollection<PlanLimit> Limits => _limits.AsReadOnly();

    private PlanVersion() { }

    public static Result<PlanVersion> CreateDraft(
        Guid planCatalogId,
        int versionNumber,
        string versionName,
        decimal monthlyPrice,
        decimal annualPriceMonthly,
        int headCapacityLimit,
        int? maxUsers = null,
        int? maxProductionUnits = null,
        IEnumerable<PlanFeature>? features = null,
        IEnumerable<PlanLimit>? limits = null)
    {
        if (versionNumber <= 0 || string.IsNullOrWhiteSpace(versionName) || monthlyPrice < 0 || annualPriceMonthly < 0 || headCapacityLimit < 0)
        {
            return Result.Failure<PlanVersion>(PlanErrors.InvalidVersionData);
        }

        var version = new PlanVersion
        {
            Id = Guid.NewGuid(),
            PlanCatalogId = planCatalogId,
            VersionNumber = versionNumber,
            VersionName = versionName.Trim(),
            Status = PlanVersionStatus.Draft,
            MonthlyPrice = monthlyPrice,
            AnnualPriceMonthly = annualPriceMonthly,
            HeadCapacityLimit = headCapacityLimit,
            MaxUsers = maxUsers,
            MaxProductionUnits = maxProductionUnits,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        if (features != null)
        {
            foreach (var f in features)
            {
                f.SetPlanVersionId(version.Id);
                version._features.Add(f);
            }
        }

        if (limits != null)
        {
            foreach (var l in limits)
            {
                l.SetPlanVersionId(version.Id);
                version._limits.Add(l);
            }
        }

        return Result.Success(version);
    }

    public Result UpdateDraft(
        string versionName,
        decimal monthlyPrice,
        decimal annualPriceMonthly,
        int headCapacityLimit,
        int? maxUsers,
        int? maxProductionUnits)
    {
        if (Status != PlanVersionStatus.Draft)
        {
            return Result.Failure(PlanErrors.PublishedVersionImmutable);
        }

        if (string.IsNullOrWhiteSpace(versionName) || monthlyPrice < 0 || annualPriceMonthly < 0 || headCapacityLimit < 0)
        {
            return Result.Failure(PlanErrors.InvalidVersionData);
        }

        VersionName = versionName.Trim();
        MonthlyPrice = monthlyPrice;
        AnnualPriceMonthly = annualPriceMonthly;
        HeadCapacityLimit = headCapacityLimit;
        MaxUsers = maxUsers;
        MaxProductionUnits = maxProductionUnits;

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
        EffectiveFrom = DateTimeOffset.UtcNow;
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
        EffectiveTo = DateTimeOffset.UtcNow;
        return Result.Success();
    }

    public Result Archive()
    {
        Status = PlanVersionStatus.Archived;
        return Result.Success();
    }
}
