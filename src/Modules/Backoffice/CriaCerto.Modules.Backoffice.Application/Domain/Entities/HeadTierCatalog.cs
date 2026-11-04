using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;

namespace CriaCerto.Modules.Backoffice.Application.Domain.Entities;

public class HeadTierCatalog
{
    public Guid Id { get; private set; }
    public string TierCode { get; private set; } = default!;
    public string DisplayName { get; private set; } = default!;
    public int MinHeads { get; private set; }
    public int MaxHeads { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAtUtc { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; private set; } = DateTimeOffset.UtcNow;

    private HeadTierCatalog() { }

    public static Result<HeadTierCatalog> Create(
        string tierCode,
        string displayName,
        int minHeads,
        int maxHeads,
        int sortOrder = 0,
        bool isActive = true)
    {
        if (string.IsNullOrWhiteSpace(tierCode) ||
            string.IsNullOrWhiteSpace(displayName) ||
            minHeads < 0 ||
            maxHeads <= 0 ||
            maxHeads < minHeads)
        {
            return Result.Failure<HeadTierCatalog>(PlanErrors.InvalidHeadTierData);
        }

        return Result.Success(new HeadTierCatalog
        {
            Id = Guid.NewGuid(),
            TierCode = tierCode.Trim().ToUpperInvariant(),
            DisplayName = displayName.Trim(),
            MinHeads = minHeads,
            MaxHeads = maxHeads,
            SortOrder = sortOrder,
            IsActive = isActive,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
    }

    public Result UpdateDetails(
        string displayName,
        int minHeads,
        int maxHeads,
        int sortOrder,
        bool isActive)
    {
        if (string.IsNullOrWhiteSpace(displayName) ||
            minHeads < 0 ||
            maxHeads <= 0 ||
            maxHeads < minHeads)
        {
            return Result.Failure(PlanErrors.InvalidHeadTierData);
        }

        DisplayName = displayName.Trim();
        MinHeads = minHeads;
        MaxHeads = maxHeads;
        SortOrder = sortOrder;
        IsActive = isActive;
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        return Result.Success();
    }
}

