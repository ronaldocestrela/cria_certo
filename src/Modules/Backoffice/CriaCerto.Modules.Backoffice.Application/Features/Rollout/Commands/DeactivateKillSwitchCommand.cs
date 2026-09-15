using System.Text.Json;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Enums;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;
using CriaCerto.Modules.Backoffice.Application.Features.Rollout.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Backoffice.Application.Features.Rollout.Commands;

public record DeactivateKillSwitchCommand(
    string FlagKey,
    string Reason,
    Guid ActorId,
    string ActorEmail,
    string? ActorRole,
    string IpAddress,
    string? UserAgent
) : IRequest<Result<FeatureFlagDto>>;

public class DeactivateKillSwitchCommandHandler : IRequestHandler<DeactivateKillSwitchCommand, Result<FeatureFlagDto>>
{
    private readonly DbContext _dbContext;

    public DeactivateKillSwitchCommandHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<FeatureFlagDto>> Handle(DeactivateKillSwitchCommand command, CancellationToken cancellationToken)
    {
        var normalizedKey = command.FlagKey.Trim().ToLowerInvariant();
        var flag = await _dbContext.Set<FeatureFlag>()
            .FirstOrDefaultAsync(f => f.Key == normalizedKey, cancellationToken);

        if (flag is null)
        {
            return Result.Failure<FeatureFlagDto>(FeatureFlagErrors.NotFound);
        }

        var oldReason = flag.KillSwitchReason;
        var restoreResult = flag.DeactivateKillSwitch(command.ActorEmail, command.Reason);
        if (restoreResult.IsFailure)
        {
            return Result.Failure<FeatureFlagDto>(restoreResult.Error);
        }

        var lastLog = await _dbContext.Set<AuditLog>()
            .OrderByDescending(a => a.TimestampUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var auditLog = AuditLog.CreateForensic(
            adminUserId: command.ActorId,
            adminUserEmail: command.ActorEmail,
            actorRole: command.ActorRole,
            action: "KILL_SWITCH_DEACTIVATED",
            category: AuditCategory.Rollout,
            severity: AuditSeverity.High,
            resource: $"FeatureFlag:{flag.Key}",
            targetTenantId: null,
            targetTenantName: null,
            ipAddress: command.IpAddress,
            userAgent: command.UserAgent,
            oldValuesJson: JsonSerializer.Serialize(new { KillSwitchActive = true, OldReason = oldReason }),
            newValuesJson: JsonSerializer.Serialize(new { KillSwitchActive = false, RestorationReason = command.Reason.Trim() }),
            previousRecordHash: lastLog?.RecordHash,
            detailsJson: JsonSerializer.Serialize(new
            {
                RestoredBy = command.ActorEmail,
                Reason = command.Reason.Trim()
            })
        );

        _dbContext.Set<AuditLog>().Add(auditLog);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var dto = new FeatureFlagDto(
            flag.Id,
            flag.Key,
            flag.Name,
            flag.Description,
            flag.Category,
            flag.IsEnabled,
            flag.RolloutPercentage,
            flag.MaxAllowedRing,
            flag.GetWhitelistedEmails(),
            flag.KillSwitchActive,
            flag.KillSwitchReason,
            flag.KillSwitchActivatedAtUtc,
            flag.KillSwitchActivatedBy,
            flag.CreatedAtUtc,
            flag.CreatedBy,
            flag.UpdatedAtUtc,
            flag.UpdatedBy,
            flag.LastToggledReason
        );

        return Result.Success(dto);
    }
}
