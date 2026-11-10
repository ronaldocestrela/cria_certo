namespace CriaCerto.Modules.Tenancy.Application.Abstractions;

public interface ITenantActiveHerdCounter
{
    Task<int> GetActiveHeadCountAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

