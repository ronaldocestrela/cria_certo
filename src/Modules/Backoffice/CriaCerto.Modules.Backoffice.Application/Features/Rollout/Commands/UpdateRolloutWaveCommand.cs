using System.Text.Json;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Enums;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;
using CriaCerto.Modules.Backoffice.Application.Features.Rollout.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Backoffice.Application.Features.Rollout.Commands;

public record UpdateRolloutWaveCommand(
    string FlagKey,
    int Percentage,
    RolloutRing MaxAllowedRing,
    List<string>? WhitelistedEmails,
    string Reason,
    Guid ActorId,
    string ActorEmail,
    string? ActorRole,
    string IpAddress,
    string? UserAgent
) : IRequest<Result<FeatureFlagDto>>;

public class UpdateRolloutWaveCommandHandler : IRequestHandler<UpdateRolloutWaveCommand, Result<FeatureFlagDto>>
{
    private readonly DbContext _dbContext;

    public UpdateRolloutWaveCommandHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<FeatureFlagDto>> Handle(UpdateRolloutWaveCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Reason) || command.Reason.Trim().Length < 10)
        {
            return Result.Failure<FeatureFlagDto>(FeatureFlagErrors.JustificationTooShort);
        }

        var normalizedKey = command.FlagKey.Trim().ToLowerInvariant();
        var flag = await _dbContext.Set<FeatureFlag>()
            .FirstOrDefaultAsync(f => f.Key == normalizedKey, cancellationToken);

        if (flag is null)
        {
            return Result.Failure<FeatureFlagDto>(FeatureFlagErrors.NotFound);
        }

        var oldStateJson = JsonSerializer.Serialize(new
        {
            flag.RolloutPercentage,
            flag.MaxAllowedRing,
            WhitelistedEmails = flag.GetWhitelistedEmails()
        });

        var updateResult = flag.UpdateRollout(
            command.Percentage,
            command.MaxAllowedRing,
            command.WhitelistedEmails,
            command.ActorEmail,
            command.Reason.Trim()
        );

        if (updateResult.IsFailure)
        {
            return Result.Failure<FeatureFlagDto>(updateResult.Error);
        }

        var newStateJson = JsonSerializer.Serialize(new
        {
            flag.RolloutPercentage,
            flag.MaxAllowedRing,
            WhitelistedEmails = flag.GetWhitelistedEmails()
        });

        var lastLog = await _dbContext.Set<AuditLog>()
            .OrderByDescending(a => a.TimestampUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var auditLog = AuditLog.CreateForensic(
            adminUserId: command.ActorId,
            adminUserEmail: command.ActorEmail,
            actorRole: command.ActorRole,
            action: "ROLLOUT_WAVE_UPDATED",
            category: AuditCategory.Rollout,
            severity: AuditSeverity.High,
            resource: $"FeatureFlag:{flag.Key}",
            targetTenantId: null,
            targetTenantName: null,
            ipAddress: command.IpAddress,
            userAgent: command.UserAgent,
            oldValuesJson: oldStateJson,
            newValuesJson: newStateJson,
            previousRecordHash: lastLog?.RecordHash,
            detailsJson: JsonSerializer.Serialize(new { Justification = command.Reason.Trim() })
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
