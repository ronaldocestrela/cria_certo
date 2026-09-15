namespace CriaCerto.Modules.Backoffice.Application.Security;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class RequireFeatureFlagAttribute : Attribute
{
    public string FeatureFlagKey { get; }

    public RequireFeatureFlagAttribute(string featureFlagKey)
    {
        FeatureFlagKey = featureFlagKey;
    }
}
