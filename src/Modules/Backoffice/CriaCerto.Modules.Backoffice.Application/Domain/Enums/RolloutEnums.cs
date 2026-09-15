namespace CriaCerto.Modules.Backoffice.Application.Domain.Enums;

public enum RolloutRing
{
    Ring0_Canary = 0,
    Ring1_EarlyAdopters = 1,
    Ring2_GeneralAvailability = 2
}

public enum FeatureFlagCategory
{
    CriticalOperation = 1,
    SupportTools = 2,
    CommercialAndPlans = 3,
    ComplianceAndPrivacy = 4
}
