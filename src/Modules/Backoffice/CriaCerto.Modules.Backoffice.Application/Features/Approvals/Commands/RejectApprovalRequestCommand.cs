using System.Text.Json;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;
using CriaCerto.Modules.Backoffice.Application.Features.Approvals.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Backoffice.Application.Features.Approvals.Commands;

public record RejectApprovalRequestCommand(
    Guid Id,
    string RejectionReason,
    Guid ReviewedByAdminUserId,
    string ReviewedByAdminEmail,
    string IpAddress
) : IRequest<Result<AdminApprovalRequestDetailDto>>;

public sealed class RejectApprovalRequestCommandHandler : IRequestHandler<RejectApprovalRequestCommand, Result<AdminApprovalRequestDetailDto>>
{
    private readonly DbContext _dbContext;

    public RejectApprovalRequestCommandHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<AdminApprovalRequestDetailDto>> Handle(RejectApprovalRequestCommand request, CancellationToken cancellationToken)
    {
        var approval = await _dbContext.Set<AdminApprovalRequest>()
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (approval is null)
        {
            return Result.Failure<AdminApprovalRequestDetailDto>(ApprovalErrors.NotFound);
        }

        var rejectResult = approval.Reject(request.ReviewedByAdminUserId, request.ReviewedByAdminEmail, request.RejectionReason);
        if (rejectResult.IsFailure)
        {
            return Result.Failure<AdminApprovalRequestDetailDto>(rejectResult.Error);
        }

        var auditLog = AuditLog.Create(
            request.ReviewedByAdminUserId,
            request.ReviewedByAdminEmail,
            "Approval.Rejected",
            $"ApprovalRequest/{approval.Id}",
            request.IpAddress,
            JsonSerializer.Serialize(new
            {
                approval.Id,
                approval.RequestType,
                approval.Title,
                approval.TargetResourceId,
                RequestedByAdminUserId = approval.RequestedByAdminUserId,
                ReviewedByAdminUserId = request.ReviewedByAdminUserId,
                request.RejectionReason
            }));

        _dbContext.Set<AuditLog>().Add(auditLog);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(ApprovalMapper.ToDetailDto(approval));
    }
}
