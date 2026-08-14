using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;

namespace CriaCerto.Modules.Backoffice.Application.Domain.Entities;

public class PlanCatalog
{
    private readonly List<PlanVersion> _versions = new();

    public Guid Id { get; private set; }
    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public string Category { get; private set; } = "PeDistributed";
    public bool IsArchived { get; private set; } = false;
    public DateTimeOffset CreatedAtUtc { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; private set; } = DateTimeOffset.UtcNow;

    public IReadOnlyCollection<PlanVersion> Versions => _versions.AsReadOnly();

    private PlanCatalog() { }

    public static Result<PlanCatalog> Create(string code, string name, string description, string category = "PeDistributed")
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(description))
        {
            return Result.Failure<PlanCatalog>(PlanErrors.InvalidPlanData);
        }

        return Result.Success(new PlanCatalog
        {
            Id = Guid.NewGuid(),
            Code = code.Trim().ToLowerInvariant(),
            Name = name.Trim(),
            Description = description.Trim(),
            Category = category.Trim(),
            IsArchived = false,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
    }

    public Result UpdateDetails(string name, string description, string category)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(description))
        {
            return Result.Failure(PlanErrors.InvalidPlanData);
        }

        Name = name.Trim();
        Description = description.Trim();
        Category = category.Trim();
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        return Result.Success();
    }

    public Result<PlanVersion> CreateVersion(
        string versionName,
        decimal monthlyPrice,
        decimal annualPriceMonthly,
        int headCapacityLimit,
        int? maxUsers = null,
        int? maxProductionUnits = null,
        IEnumerable<PlanFeature>? features = null,
        IEnumerable<PlanLimit>? limits = null)
    {
        if (IsArchived)
        {
            return Result.Failure<PlanVersion>(PlanErrors.InvalidPlanData);
        }

        if (_versions.Any(v => v.Status == PlanVersionStatus.Draft))
        {
            return Result.Failure<PlanVersion>(PlanErrors.DraftAlreadyExists);
        }

        int nextVersionNumber = _versions.Count > 0 ? _versions.Max(v => v.VersionNumber) + 1 : 1;

        var versionResult = PlanVersion.CreateDraft(
            Id,
            nextVersionNumber,
            versionName,
            monthlyPrice,
            annualPriceMonthly,
            headCapacityLimit,
            maxUsers,
            maxProductionUnits,
            features,
            limits);

        if (versionResult.IsFailure) return versionResult;

        _versions.Add(versionResult.Value);
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        return versionResult;
    }

    public Result PublishVersion(Guid versionId, Guid adminUserId, string? approvalNotes = null)
    {
        var targetVersion = _versions.FirstOrDefault(v => v.Id == versionId);
        if (targetVersion is null)
        {
            return Result.Failure(PlanErrors.VersionNotFound);
        }

        // Deprecate existing Published version
        var currentPublished = _versions.FirstOrDefault(v => v.Status == PlanVersionStatus.Published);
        if (currentPublished != null && currentPublished.Id != versionId)
        {
            var deprecateResult = currentPublished.Deprecate();
            if (deprecateResult.IsFailure) return deprecateResult;
        }

        var publishResult = targetVersion.Publish(adminUserId, approvalNotes);
        if (publishResult.IsFailure) return publishResult;

        UpdatedAtUtc = DateTimeOffset.UtcNow;
        return Result.Success();
    }

    public Result DeprecateVersion(Guid versionId)
    {
        var targetVersion = _versions.FirstOrDefault(v => v.Id == versionId);
        if (targetVersion is null)
        {
            return Result.Failure(PlanErrors.VersionNotFound);
        }

        var result = targetVersion.Deprecate();
        if (result.IsSuccess) UpdatedAtUtc = DateTimeOffset.UtcNow;
        return result;
    }

    public Result Archive()
    {
        IsArchived = true;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        return Result.Success();
    }
}
