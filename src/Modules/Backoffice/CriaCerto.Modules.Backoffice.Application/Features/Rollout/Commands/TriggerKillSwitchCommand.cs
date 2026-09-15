using System.Text.Json;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Enums;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;
using CriaCerto.Modules.Backoffice.Application.Features.Rollout.Dtos;
using CriaCerto.Modules.Backoffice.Application.Telemetry;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Backoffice.Application.Features.Rollout.Commands;

public record TriggerKillSwitchCommand(
    string FlagKey,
    string Reason,
    Guid ActorId,
    string ActorEmail,
    string? ActorRole,
    string IpAddress,
    string? UserAgent
) : IRequest<Result<FeatureFlagDto>>;

public class TriggerKillSwitchCommandHandler : IRequestHandler<TriggerKillSwitchCommand, Result<FeatureFlagDto>>
{
    private readonly DbContext _dbContext;

    public TriggerKillSwitchCommandHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<FeatureFlagDto>> Handle(TriggerKillSwitchCommand command, CancellationToken cancellationToken)
    {
        var normalizedKey = command.FlagKey.Trim().ToLowerInvariant();
        var flag = await _dbContext.Set<FeatureFlag>()
            .FirstOrDefaultAsync(f => f.Key == normalizedKey, cancellationToken);

        if (flag is null)
        {
            return Result.Failure<FeatureFlagDto>(FeatureFlagErrors.NotFound);
        }

        var killResult = flag.TriggerKillSwitch(command.Reason, command.ActorEmail);
        if (killResult.IsFailure)
        {
            return Result.Failure<FeatureFlagDto>(killResult.Error);
        }

        var lastLog = await _dbContext.Set<AuditLog>()
            .OrderByDescending(a => a.TimestampUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var auditLog = AuditLog.CreateForensic(
            adminUserId: command.ActorId,
            adminUserEmail: command.ActorEmail,
            actorRole: command.ActorRole,
            action: "KILL_SWITCH_ACTIVATED",
            category: AuditCategory.Rollout,
            severity: AuditSeverity.Critical,
            resource: $"FeatureFlag:{flag.Key}",
            targetTenantId: null,
            targetTenantName: null,
            ipAddress: command.IpAddress,
            userAgent: command.UserAgent,
            oldValuesJson: JsonSerializer.Serialize(new { KillSwitchActive = false }),
            newValuesJson: JsonSerializer.Serialize(new { KillSwitchActive = true, Reason = command.Reason.Trim() }),
            previousRecordHash: lastLog?.RecordHash,
            detailsJson: JsonSerializer.Serialize(new
            {
                TriggeredBy = command.ActorEmail,
                Reason = command.Reason.Trim(),
                EmergencyLevel = "CRITICAL_CUTOFF"
            })
        );

        _dbContext.Set<AuditLog>().Add(auditLog);
        await _dbContext.SaveChangesAsync(cancellationToken);

        BackofficeTelemetry.RecordAction("Rollout", "Critical", command.ActorRole ?? "PlatformOwner");

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
