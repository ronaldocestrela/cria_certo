using System.Text.Json;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Features.Approvals.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Backoffice.Application.Features.Approvals.Commands;

public record CreateApprovalRequestCommand(
    ApprovalRequestType RequestType,
    string Title,
    string Justification,
    string TargetResourceId,
    string ImpactSummary,
    string PayloadJson,
    Guid RequestedByAdminUserId,
    string RequestedByAdminEmail,
    string IpAddress,
    string? SupportTicketId = null,
    string? DiffJson = null,
    int? ExpirationHours = null
) : IRequest<Result<AdminApprovalRequestDetailDto>>;

public sealed class CreateApprovalRequestCommandHandler : IRequestHandler<CreateApprovalRequestCommand, Result<AdminApprovalRequestDetailDto>>
{
    private readonly DbContext _dbContext;

    public CreateApprovalRequestCommandHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<AdminApprovalRequestDetailDto>> Handle(CreateApprovalRequestCommand request, CancellationToken cancellationToken)
    {
        var creationResult = AdminApprovalRequest.Create(
            request.RequestType,
            request.Title,
            request.Justification,
            request.TargetResourceId,
            request.ImpactSummary,
            request.PayloadJson,
            request.RequestedByAdminUserId,
            request.RequestedByAdminEmail,
            request.SupportTicketId,
            request.DiffJson,
            request.ExpirationHours ?? AdminApprovalRequest.DefaultExpirationHours);

        if (creationResult.IsFailure)
        {
            return Result.Failure<AdminApprovalRequestDetailDto>(creationResult.Error);
        }

        var approvalRequest = creationResult.Value;

        _dbContext.Set<AdminApprovalRequest>().Add(approvalRequest);

        var auditLog = AuditLog.Create(
            request.RequestedByAdminUserId,
            request.RequestedByAdminEmail,
            "Approval.Requested",
            $"ApprovalRequest/{approvalRequest.Id}",
            request.IpAddress,
            JsonSerializer.Serialize(new
            {
                approvalRequest.Id,
                approvalRequest.RequestType,
                approvalRequest.Title,
                approvalRequest.TargetResourceId,
                approvalRequest.SupportTicketId,
                approvalRequest.ExpiresAtUtc
            }));

        _dbContext.Set<AuditLog>().Add(auditLog);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(ApprovalMapper.ToDetailDto(approvalRequest));
    }
}
