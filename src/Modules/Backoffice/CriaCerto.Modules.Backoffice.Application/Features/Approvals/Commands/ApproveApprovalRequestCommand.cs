using System.Text.Json;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;
using CriaCerto.Modules.Backoffice.Application.Features.Approvals.Dtos;
using CriaCerto.Modules.Tenancy.Application.Features.BackofficeTenants;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Backoffice.Application.Features.Approvals.Commands;

public record ApproveApprovalRequestCommand(
    Guid Id,
    Guid ReviewedByAdminUserId,
    string ReviewedByAdminEmail,
    string IpAddress,
    string? ReviewNotes = null
) : IRequest<Result<AdminApprovalRequestDetailDto>>;

public sealed class ApproveApprovalRequestCommandHandler : IRequestHandler<ApproveApprovalRequestCommand, Result<AdminApprovalRequestDetailDto>>
{
    private readonly DbContext _dbContext;
    private readonly ISender _sender;

    public ApproveApprovalRequestCommandHandler(DbContext dbContext, ISender sender)
    {
        _dbContext = dbContext;
        _sender = sender;
    }

    public async Task<Result<AdminApprovalRequestDetailDto>> Handle(ApproveApprovalRequestCommand request, CancellationToken cancellationToken)
    {
        var approval = await _dbContext.Set<AdminApprovalRequest>()
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (approval is null)
        {
            return Result.Failure<AdminApprovalRequestDetailDto>(ApprovalErrors.NotFound);
        }

        // Domain validation including strict 4-Eyes Principle (requester != reviewer)
        var approveResult = approval.Approve(request.ReviewedByAdminUserId, request.ReviewedByAdminEmail, request.ReviewNotes);
        if (approveResult.IsFailure)
        {
            return Result.Failure<AdminApprovalRequestDetailDto>(approveResult.Error);
        }

        // Execute action based on payload
        var executionResult = await ExecuteActionAsync(approval, cancellationToken);
        if (executionResult.IsFailure)
        {
            approval.MarkAsExecutionFailed(executionResult.Error.Message);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Result.Failure<AdminApprovalRequestDetailDto>(executionResult.Error);
        }

        approval.MarkAsExecuted(executionResult.Value);

        var auditLog = AuditLog.Create(
            request.ReviewedByAdminUserId,
            request.ReviewedByAdminEmail,
            "Approval.ApprovedAndExecuted",
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
                request.ReviewNotes,
                ExecutionResult = executionResult.Value
            }));

        _dbContext.Set<AuditLog>().Add(auditLog);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(ApprovalMapper.ToDetailDto(approval));
    }

    private async Task<Result<string>> ExecuteActionAsync(AdminApprovalRequest approval, CancellationToken cancellationToken)
    {
        try
        {
            switch (approval.RequestType)
            {
                case ApprovalRequestType.PublishPlanVersion:
                {
                    using var doc = JsonDocument.Parse(approval.PayloadJson);
                    if (!doc.RootElement.TryGetProperty("VersionId", out var versionProp) ||
                        !Guid.TryParse(versionProp.GetString(), out var versionId))
                    {
                        return Result.Failure<string>(ApprovalErrors.ExecutionFailed);
                    }

                    var notes = doc.RootElement.TryGetProperty("ApprovalNotes", out var notesProp)
                        ? notesProp.GetString()
                        : approval.ReviewNotes;

                    var plan = await _dbContext.Set<PlanCatalog>()
                        .Include(p => p.Versions)
                        .FirstOrDefaultAsync(p => p.Versions.Any(v => v.Id == versionId), cancellationToken);

                    if (plan is null)
                    {
                        return Result.Failure<string>(PlanErrors.VersionNotFound);
                    }

                    var publishResult = plan.PublishVersion(versionId, approval.ReviewedByAdminUserId!.Value, notes);
                    if (publishResult.IsFailure)
                    {
                        return Result.Failure<string>(publishResult.Error);
                    }

                    return Result.Success(JsonSerializer.Serialize(new { PlanCatalogId = plan.Id, VersionId = versionId, Status = "Published" }));
                }

                case ApprovalRequestType.MassTenantSuspension:
                {
                    using var doc = JsonDocument.Parse(approval.PayloadJson);
                    if (!doc.RootElement.TryGetProperty("TenantIds", out var tenantIdsProp) ||
                        tenantIdsProp.ValueKind != JsonValueKind.Array)
                    {
                        return Result.Failure<string>(ApprovalErrors.ExecutionFailed);
                    }

                    var reason = doc.RootElement.TryGetProperty("Reason", out var reasonProp)
                        ? reasonProp.GetString() ?? approval.Justification
                        : approval.Justification;

                    var suspendedTenants = new List<Guid>();
                    foreach (var elem in tenantIdsProp.EnumerateArray())
                    {
                        if (Guid.TryParse(elem.GetString(), out var tenantId))
                        {
                            var suspendCmd = new SuspendTenantForAdminCommand(tenantId, $"[4-Eyes Approval {approval.Id}] {reason}");
                            var cmdResult = await _sender.Send(suspendCmd, cancellationToken);
                            if (cmdResult.IsSuccess)
                            {
                                suspendedTenants.Add(tenantId);
                            }
                        }
                    }

                    return Result.Success(JsonSerializer.Serialize(new { TotalRequested = tenantIdsProp.GetArrayLength(), TotalSuspended = suspendedTenants.Count, SuspendedTenantIds = suspendedTenants }));
                }

                case ApprovalRequestType.ExtendedAccessGrant:
                {
                    using var doc = JsonDocument.Parse(approval.PayloadJson);
                    if (!doc.RootElement.TryGetProperty("AdminUserId", out var userProp) ||
                        !Guid.TryParse(userProp.GetString(), out var targetUserId))
                    {
                        return Result.Failure<string>(ApprovalErrors.ExecutionFailed);
                    }

                    var adminUser = await _dbContext.Set<AdminUser>()
                        .Include(u => u.Roles)
                        .FirstOrDefaultAsync(u => u.Id == targetUserId, cancellationToken);

                    if (adminUser is null)
                    {
                        return Result.Failure<string>(BackofficeErrors.UserNotFound);
                    }

                    if (doc.RootElement.TryGetProperty("RoleIds", out var rolesProp) && rolesProp.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var roleElem in rolesProp.EnumerateArray())
                        {
                            if (Guid.TryParse(roleElem.GetString(), out var roleId))
                            {
                                var role = await _dbContext.Set<AdminRole>().FindAsync(new object[] { roleId }, cancellationToken);
                                if (role is not null)
                                {
                                    adminUser.AssignRole(role);
                                }
                            }
                        }
                    }

                    return Result.Success(JsonSerializer.Serialize(new { AdminUserId = targetUserId, GrantedRolesCount = adminUser.Roles.Count }));
                }

                default:
                    return Result.Success(JsonSerializer.Serialize(new { Executed = true, Type = approval.RequestType.ToString() }));
            }
        }
        catch (Exception ex)
        {
            return Result.Failure<string>(Error.Failure("Approvals.ExecutionException", ex.Message));
        }
    }
}
