using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Domain.Errors;

namespace CriaCerto.Modules.Tenancy.Application.Abstractions;

public interface ITenantAccessGuard
{
    Task<Result> EnsureProducerAccessAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
