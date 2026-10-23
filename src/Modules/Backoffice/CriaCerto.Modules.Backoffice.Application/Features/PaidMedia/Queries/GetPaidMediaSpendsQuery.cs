using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Features.PaidMedia.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Backoffice.Application.Features.PaidMedia.Queries;

public record GetPaidMediaSpendsQuery(
    DateTime? StartDateUtc = null,
    DateTime? EndDateUtc = null
) : IRequest<Result<IReadOnlyList<PaidMediaSpendDto>>>;

public sealed class GetPaidMediaSpendsQueryHandler : IRequestHandler<GetPaidMediaSpendsQuery, Result<IReadOnlyList<PaidMediaSpendDto>>>
{
    private readonly DbContext _dbContext;

    public GetPaidMediaSpendsQueryHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<IReadOnlyList<PaidMediaSpendDto>>> Handle(GetPaidMediaSpendsQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.Set<PaidMediaSpend>().AsNoTracking();

        if (request.StartDateUtc.HasValue)
        {
            query = query.Where(s => s.PeriodEndDateUtc >= request.StartDateUtc.Value);
        }

        if (request.EndDateUtc.HasValue)
        {
            query = query.Where(s => s.PeriodStartDateUtc <= request.EndDateUtc.Value);
        }

        var spends = await query
            .OrderByDescending(s => s.PeriodStartDateUtc)
            .ToListAsync(cancellationToken);

        var dtos = spends.Select(spend =>
        {
            var cacDirect = spend.AcquisitionsCount > 0 ? Math.Round(spend.Amount / spend.AcquisitionsCount, 2) : 0m;
            return new PaidMediaSpendDto(
                spend.Id,
                spend.Channel.ToString(),
                spend.PeriodStartDateUtc,
                spend.PeriodEndDateUtc,
                spend.Amount,
                spend.AcquisitionsCount,
                cacDirect,
                spend.Notes,
                spend.CreatedByAdminId,
                spend.CreatedAtUtc);
        }).ToList();

        return Result.Success<IReadOnlyList<PaidMediaSpendDto>>(dtos);
    }
}
