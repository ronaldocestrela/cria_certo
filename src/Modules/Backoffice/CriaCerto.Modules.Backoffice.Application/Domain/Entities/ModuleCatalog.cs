using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;

namespace CriaCerto.Modules.Backoffice.Application.Domain.Entities;

public class ModuleCatalog
{
    private readonly List<ModuleTierPrice> _tierPrices = new();

    public Guid Id { get; private set; }
    public string ModuleKey { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public string Icon { get; private set; } = "widgets";
    public bool IsCore { get; private set; }
    public bool IsActive { get; private set; } = true;
    public int SortOrder { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; private set; } = DateTimeOffset.UtcNow;

    public IReadOnlyCollection<ModuleTierPrice> TierPrices => _tierPrices.AsReadOnly();

    private ModuleCatalog() { }

    public static Result<ModuleCatalog> Create(
        string moduleKey,
        string name,
        string description,
        string icon = "widgets",
        bool isCore = false,
        int sortOrder = 0,
        bool isActive = true)
    {
        if (string.IsNullOrWhiteSpace(moduleKey) ||
            string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(description))
        {
            return Result.Failure<ModuleCatalog>(PlanErrors.InvalidModuleCatalogData);
        }

        if (isCore && !isActive)
        {
            return Result.Failure<ModuleCatalog>(PlanErrors.CannotDeactivateCoreModule);
        }

        return Result.Success(new ModuleCatalog
        {
            Id = Guid.NewGuid(),
            ModuleKey = moduleKey.Trim(),
            Name = name.Trim(),
            Description = description.Trim(),
            Icon = string.IsNullOrWhiteSpace(icon) ? "widgets" : icon.Trim(),
            IsCore = isCore,
            IsActive = isActive,
            SortOrder = sortOrder,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
    }

    public Result UpdateDetails(
        string name,
        string description,
        string icon,
        bool isCore,
        int sortOrder,
        bool isActive)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(description))
        {
            return Result.Failure(PlanErrors.InvalidModuleCatalogData);
        }

        if ((IsCore || isCore) && !isActive)
        {
            return Result.Failure(PlanErrors.CannotDeactivateCoreModule);
        }

        Name = name.Trim();
        Description = description.Trim();
        Icon = string.IsNullOrWhiteSpace(icon) ? "widgets" : icon.Trim();
        IsCore = isCore;
        SortOrder = sortOrder;
        IsActive = isActive;
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        return Result.Success();
    }

    public Result<ModuleTierPrice> CreateTierPriceDraft(
        Guid headTierCatalogId,
        decimal monthlyPrice,
        decimal annualPriceMonthly,
        string? stripePriceIdMonthly = null,
        string? stripePriceIdAnnual = null)
    {
        var tierPricesForTier = _tierPrices.Where(p => p.HeadTierCatalogId == headTierCatalogId).ToList();

        if (tierPricesForTier.Any(p => p.Status == PlanVersionStatus.Draft))
        {
            return Result.Failure<ModuleTierPrice>(PlanErrors.DraftAlreadyExists);
        }

        int nextVersionNumber = tierPricesForTier.Count > 0
            ? tierPricesForTier.Max(p => p.VersionNumber) + 1
            : 1;

        var priceResult = ModuleTierPrice.CreateDraft(
            Id,
            headTierCatalogId,
            nextVersionNumber,
            monthlyPrice,
            annualPriceMonthly,
            stripePriceIdMonthly,
            stripePriceIdAnnual);

        if (priceResult.IsFailure)
        {
            return priceResult;
        }

        _tierPrices.Add(priceResult.Value);
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        return priceResult;
    }

    public Result PublishTierPrice(Guid tierPriceId, Guid adminUserId, string? approvalNotes = null)
    {
        var targetPrice = _tierPrices.FirstOrDefault(p => p.Id == tierPriceId);
        if (targetPrice is null)
        {
            return Result.Failure(PlanErrors.ModuleTierPriceNotFound);
        }

        var currentPublished = _tierPrices.FirstOrDefault(p =>
            p.HeadTierCatalogId == targetPrice.HeadTierCatalogId &&
            p.Status == PlanVersionStatus.Published &&
            p.Id != tierPriceId);

        if (currentPublished is not null)
        {
            var deprecateResult = currentPublished.Deprecate();
            if (deprecateResult.IsFailure)
            {
                return deprecateResult;
            }
        }

        var publishResult = targetPrice.Publish(adminUserId, approvalNotes);
        if (publishResult.IsFailure)
        {
            return publishResult;
        }

        UpdatedAtUtc = DateTimeOffset.UtcNow;
        return Result.Success();
    }

    public Result DeprecateTierPrice(Guid tierPriceId)
    {
        var targetPrice = _tierPrices.FirstOrDefault(p => p.Id == tierPriceId);
        if (targetPrice is null)
        {
            return Result.Failure(PlanErrors.ModuleTierPriceNotFound);
        }

        var result = targetPrice.Deprecate();
        if (result.IsSuccess)
        {
            UpdatedAtUtc = DateTimeOffset.UtcNow;
        }

        return result;
    }
}

