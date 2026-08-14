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
        var status = await _dbContext.Tenants
            .AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => t.Status)
            .FirstOrDefaultAsync(cancellationToken);

        if (status is null)
        {
            return Result.Failure(TenancyErrors.TenantNotFound);
        }

        if (!TenantLifecycle.CanProducerAccess(status))
        {
            return Result.Failure(TenancyErrors.TenantNotAccessible);
        }

        return Result.Success();
    }
}
