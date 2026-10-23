using CriaCerto.Modules.Backoffice.Infrastructure.Persistence;
using CriaCerto.Modules.Referral.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Api.Services;

public sealed class PaidMediaSpendLookupService : IPaidMediaSpendLookupService
{
    private readonly BackofficeDbContext _backofficeDbContext;

    public PaidMediaSpendLookupService(BackofficeDbContext backofficeDbContext)
    {
        _backofficeDbContext = backofficeDbContext;
    }

    public async Task<PaidMediaSummaryDto> GetPaidMediaSummaryAsync(
        DateTime? startDateUtc,
        DateTime? endDateUtc,
        CancellationToken cancellationToken = default)
    {
        var query = _backofficeDbContext.PaidMediaSpends.AsNoTracking();

        if (startDateUtc.HasValue)
        {
            query = query.Where(s => s.PeriodEndDateUtc >= startDateUtc.Value);
        }

        if (endDateUtc.HasValue)
        {
            query = query.Where(s => s.PeriodStartDateUtc <= endDateUtc.Value);
        }

        var spends = await query.ToListAsync(cancellationToken);

        decimal totalSpent = spends.Sum(s => s.Amount);
        int totalAcquisitions = spends.Sum(s => s.AcquisitionsCount);

        return new PaidMediaSummaryDto(totalSpent, totalAcquisitions);
    }
}
