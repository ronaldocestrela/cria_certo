using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;
using CriaCerto.Modules.Backoffice.Application.Features.Approvals.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Backoffice.Application.Features.Approvals.Queries;

public record GetApprovalRequestsQuery(
    ApprovalRequestStatus? Status = null,
    ApprovalRequestType? RequestType = null,
    Guid? RequestedByAdminUserId = null,
    Guid? ReviewerId = null,
    int Page = 1,
    int PageSize = 20
) : IRequest<Result<PagedApprovalResult>>;

public record GetApprovalRequestByIdQuery(Guid Id) : IRequest<Result<AdminApprovalRequestDetailDto>>;

public record GetPendingApprovalsCountQuery(Guid CurrentAdminUserId) : IRequest<Result<PendingApprovalsCountDto>>;

public sealed class GetApprovalRequestsQueryHandler :
    IRequestHandler<GetApprovalRequestsQuery, Result<PagedApprovalResult>>,
    IRequestHandler<GetApprovalRequestByIdQuery, Result<AdminApprovalRequestDetailDto>>,
    IRequestHandler<GetPendingApprovalsCountQuery, Result<PendingApprovalsCountDto>>
{
    private readonly DbContext _dbContext;

    public GetApprovalRequestsQueryHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<PagedApprovalResult>> Handle(GetApprovalRequestsQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 20 : Math.Min(request.PageSize, 100);

        var query = _dbContext.Set<AdminApprovalRequest>().AsNoTracking().AsQueryable();

        if (request.Status.HasValue)
        {
            query = query.Where(r => r.Status == request.Status.Value);
        }

        if (request.RequestType.HasValue)
        {
            query = query.Where(r => r.RequestType == request.RequestType.Value);
        }

        if (request.RequestedByAdminUserId.HasValue)
        {
            query = query.Where(r => r.RequestedByAdminUserId == request.RequestedByAdminUserId.Value);
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(r => r.RequestedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // Auto-check expiration for pending items
        foreach (var item in items)
        {
            item.CheckAndMarkExpired();
        }

        var dtos = items.Select(ApprovalMapper.ToSummaryDto).ToList();

        return Result.Success(new PagedApprovalResult(dtos, total, page, pageSize));
    }

    public async Task<Result<AdminApprovalRequestDetailDto>> Handle(GetApprovalRequestByIdQuery request, CancellationToken cancellationToken)
    {
        var approval = await _dbContext.Set<AdminApprovalRequest>()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (approval is null)
        {
            return Result.Failure<AdminApprovalRequestDetailDto>(ApprovalErrors.NotFound);
        }

        approval.CheckAndMarkExpired();

        return Result.Success(ApprovalMapper.ToDetailDto(approval));
    }

    public async Task<Result<PendingApprovalsCountDto>> Handle(GetPendingApprovalsCountQuery request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var pendingQuery = _dbContext.Set<AdminApprovalRequest>()
            .AsNoTracking()
            .Where(r => r.Status == ApprovalRequestStatus.Pending && r.ExpiresAtUtc > now);

        var totalPending = await pendingQuery.CountAsync(cancellationToken);
        var myPending = await pendingQuery.CountAsync(r => r.RequestedByAdminUserId == request.CurrentAdminUserId, cancellationToken);
        var pendingForMe = await pendingQuery.CountAsync(r => r.RequestedByAdminUserId != request.CurrentAdminUserId, cancellationToken);

        return Result.Success(new PendingApprovalsCountDto(totalPending, myPending, pendingForMe));
    }
}
