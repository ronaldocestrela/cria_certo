using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Domain;
using CriaCerto.Modules.Tenancy.Application.Domain.Errors;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Tenancy.Infrastructure.Services;

public sealed class TenantAccessGuard : ITenantAccessGuard
{
    private readonly ITenancyDbContext _dbContext;

    public TenantAccessGuard(ITenancyDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result> EnsureProducerAccessAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenantData = await _dbContext.Tenants
            .AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => new { t.Status, t.CurrentPeriodEndUtc })
            .FirstOrDefaultAsync(cancellationToken);

        if (tenantData is null)
        {
            return Result.Failure(TenancyErrors.TenantNotFound);
        }

        if (string.Equals(tenantData.Status, TenantLifecycle.ToStatusString(TenantStatus.Trial), StringComparison.OrdinalIgnoreCase)
            && tenantData.CurrentPeriodEndUtc.HasValue
            && tenantData.CurrentPeriodEndUtc.Value < DateTime.UtcNow)
        {
            return Result.Failure(TenancyErrors.TrialExpired);
        }

        if (!TenantLifecycle.CanProducerAccess(tenantData.Status))
        {
            return Result.Failure(TenancyErrors.TenantNotAccessible);
        }

        return Result.Success();
    }
}
