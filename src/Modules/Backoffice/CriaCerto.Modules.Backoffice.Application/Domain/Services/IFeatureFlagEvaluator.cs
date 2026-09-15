using CriaCerto.Modules.Backoffice.Application.Domain.Entities;

namespace CriaCerto.Modules.Backoffice.Application.Domain.Services;

public interface IFeatureFlagEvaluator
{
    bool Evaluate(FeatureFlag flag, string adminEmail, string actorRole, Guid? tenantId = null);
}
