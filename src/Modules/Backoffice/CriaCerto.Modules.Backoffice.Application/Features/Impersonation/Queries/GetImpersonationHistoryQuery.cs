using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Features.Impersonation.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Backoffice.Application.Features.Impersonation.Queries;

public record GetImpersonationHistoryQuery(
    Guid? TenantId = null,
    Guid? AdminUserId = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedImpersonationAuditResult>>;

public record PagedImpersonationAuditResult(
    IReadOnlyCollection<ImpersonationAuditItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public class GetImpersonationHistoryQueryHandler : IRequestHandler<GetImpersonationHistoryQuery, Result<PagedImpersonationAuditResult>>
{
    private readonly DbContext _dbContext;

    public GetImpersonationHistoryQueryHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<PagedImpersonationAuditResult>> Handle(
        GetImpersonationHistoryQuery query,
        CancellationToken cancellationToken)
    {
        var dbQuery = _dbContext.Set<ImpersonationSession>().AsNoTracking();

        if (query.TenantId.HasValue && query.TenantId.Value != Guid.Empty)
        {
            dbQuery = dbQuery.Where(s => s.TargetTenantId == query.TenantId.Value);
        }

        if (query.AdminUserId.HasValue && query.AdminUserId.Value != Guid.Empty)
        {
            dbQuery = dbQuery.Where(s => s.AdminUserId == query.AdminUserId.Value);
        }

        var totalCount = await dbQuery.CountAsync(cancellationToken);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var items = await dbQuery
            .OrderByDescending(s => s.StartedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new ImpersonationAuditItemDto(
                s.Id,
                s.AdminUserId,
                s.AdminUserEmail,
                s.TargetTenantId,
                s.TargetTenantName,
                s.TargetUserId,
                s.TargetUserEmail,
                s.SupportTicket,
                s.Justification,
                s.DurationMinutes,
                s.StartedAtUtc,
                s.ExpiresAtUtc,
                s.EndedAtUtc,
                s.Status.ToString(),
                s.IpAddress,
                s.RevocationReason))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedImpersonationAuditResult(items, totalCount, page, pageSize));
    }
}
