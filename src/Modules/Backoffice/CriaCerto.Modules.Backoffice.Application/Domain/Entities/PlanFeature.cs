namespace CriaCerto.Modules.Backoffice.Application.Domain.Entities;

public class PlanFeature
{
    public Guid Id { get; private set; }
    public Guid PlanVersionId { get; private set; }
    public string FeatureKey { get; private set; } = default!;
    public string DisplayName { get; private set; } = default!;
    public bool IsEnabled { get; private set; } = true;
    public string FeatureType { get; private set; } = "ModuleAccess";

    private PlanFeature() { }

    public static PlanFeature Create(string featureKey, string displayName, bool isEnabled = true, string featureType = "ModuleAccess")
    {
        return new PlanFeature
        {
            Id = Guid.NewGuid(),
            FeatureKey = featureKey.Trim(),
            DisplayName = displayName.Trim(),
            IsEnabled = isEnabled,
            FeatureType = featureType.Trim()
        };
    }

    public void SetPlanVersionId(Guid planVersionId)
    {
        PlanVersionId = planVersionId;
    }
}
