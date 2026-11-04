using System.Collections.Generic;

namespace CriaCerto.BuildingBlocks.Abstractions.Tenancy;

public interface ITenantContext
{
    Guid? TenantId { get; }
    string? SubscribedPlan { get; }
    string? HeadTier => null;
    int HeadCapacity { get; }
    IReadOnlySet<string> SubscribedModules { get; }
}
