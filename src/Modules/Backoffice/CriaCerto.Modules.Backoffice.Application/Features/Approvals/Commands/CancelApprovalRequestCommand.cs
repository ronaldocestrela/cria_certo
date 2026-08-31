using System.Text.Json;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;
using CriaCerto.Modules.Backoffice.Application.Features.Approvals.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Backoffice.Application.Features.Approvals.Commands;

public record CancelApprovalRequestCommand(
    Guid Id,
    Guid RequestedByAdminUserId,
    string RequestedByAdminEmail,
    string IpAddress,
    string? CancelReason = null
) : IRequest<Result<AdminApprovalRequestDetailDto>>;

public sealed class CancelApprovalRequestCommandHandler : IRequestHandler<CancelApprovalRequestCommand, Result<AdminApprovalRequestDetailDto>>
{
    private readonly DbContext _dbContext;

    public CancelApprovalRequestCommandHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<AdminApprovalRequestDetailDto>> Handle(CancelApprovalRequestCommand request, CancellationToken cancellationToken)
    {
        var approval = await _dbContext.Set<AdminApprovalRequest>()
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (approval is null)
        {
            return Result.Failure<AdminApprovalRequestDetailDto>(ApprovalErrors.NotFound);
        }

        var cancelResult = approval.Cancel(request.RequestedByAdminUserId, request.CancelReason);
        if (cancelResult.IsFailure)
        {
            return Result.Failure<AdminApprovalRequestDetailDto>(cancelResult.Error);
        }

        var auditLog = AuditLog.Create(
            request.RequestedByAdminUserId,
            request.RequestedByAdminEmail,
            "Approval.Cancelled",
            $"ApprovalRequest/{approval.Id}",
            request.IpAddress,
            JsonSerializer.Serialize(new
            {
                approval.Id,
                approval.RequestType,
                approval.Title,
                approval.TargetResourceId,
                request.CancelReason
            }));

        _dbContext.Set<AuditLog>().Add(auditLog);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(ApprovalMapper.ToDetailDto(approval));
    }
}
