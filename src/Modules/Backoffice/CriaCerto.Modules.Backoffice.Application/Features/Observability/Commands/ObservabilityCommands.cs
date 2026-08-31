using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Enums;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;
using CriaCerto.Modules.Backoffice.Application.Features.Observability.Dtos;
using CriaCerto.Modules.Backoffice.Application.Telemetry;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Backoffice.Application.Features.Observability.Commands;

public record AcknowledgeBackofficeAlertCommand(
    Guid AlertId,
    Guid AdminUserId,
    string AdminEmail) : IRequest<Result>;

public class AcknowledgeBackofficeAlertCommandHandler : IRequestHandler<AcknowledgeBackofficeAlertCommand, Result>
{
    private readonly DbContext _dbContext;

    public AcknowledgeBackofficeAlertCommandHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result> Handle(AcknowledgeBackofficeAlertCommand request, CancellationToken cancellationToken)
    {
        var alert = await _dbContext.Set<BackofficeAlert>()
            .FirstOrDefaultAsync(a => a.Id == request.AlertId, cancellationToken);

        if (alert is null)
        {
            return Result.Failure(ObservabilityErrors.AlertNotFound);
        }

        var result = alert.Acknowledge(request.AdminUserId, request.AdminEmail);
        if (result.IsFailure)
        {
            return result;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public record ResolveBackofficeAlertCommand(
    Guid AlertId,
    Guid AdminUserId,
    string AdminEmail,
    string ResolutionNotes) : IRequest<Result>;

public class ResolveBackofficeAlertCommandHandler : IRequestHandler<ResolveBackofficeAlertCommand, Result>
{
    private readonly DbContext _dbContext;

    public ResolveBackofficeAlertCommandHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result> Handle(ResolveBackofficeAlertCommand request, CancellationToken cancellationToken)
    {
        var alert = await _dbContext.Set<BackofficeAlert>()
            .FirstOrDefaultAsync(a => a.Id == request.AlertId, cancellationToken);

        if (alert is null)
        {
            return Result.Failure(ObservabilityErrors.AlertNotFound);
        }

        var result = alert.Resolve(request.AdminUserId, request.AdminEmail, request.ResolutionNotes);
        if (result.IsFailure)
        {
            return result;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public record SimulateBackofficeAlertCommand(
    string RuleCode,
    AlertSeverity Severity,
    string Title,
    string Description,
    string? ContextJson = null) : IRequest<Result<BackofficeAlertDto>>;

public class SimulateBackofficeAlertCommandHandler : IRequestHandler<SimulateBackofficeAlertCommand, Result<BackofficeAlertDto>>
{
    private readonly DbContext _dbContext;

    public SimulateBackofficeAlertCommandHandler(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<BackofficeAlertDto>> Handle(SimulateBackofficeAlertCommand request, CancellationToken cancellationToken)
    {
        var fingerprint = $"{request.RuleCode}:simulated:{Guid.NewGuid():N}";
        var createResult = BackofficeAlert.Create(
            ruleCode: request.RuleCode,
            title: request.Title,
            description: request.Description,
            severity: request.Severity,
            fingerprint: fingerprint,
            contextJson: request.ContextJson ?? "{\"simulation\":true}");

        if (createResult.IsFailure)
        {
            return Result.Failure<BackofficeAlertDto>(createResult.Error);
        }

        var alert = createResult.Value;
        await _dbContext.Set<BackofficeAlert>().AddAsync(alert, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        BackofficeTelemetry.RecordAlert(alert.RuleCode, alert.Severity.ToString());

        var dto = new BackofficeAlertDto(
            alert.Id,
            alert.RuleCode,
            alert.Title,
            alert.Description,
            alert.Severity,
            alert.Status,
            alert.Fingerprint,
            alert.OccurrenceCount,
            alert.FirstTriggeredAtUtc,
            alert.LastTriggeredAtUtc,
            alert.ContextJson,
            alert.TargetTenantId,
            alert.TargetTenantName,
            alert.RelatedAdminUserId,
            alert.RelatedAdminEmail,
            alert.AcknowledgedAtUtc,
            alert.AcknowledgedByEmail,
            alert.ResolvedAtUtc,
            alert.ResolvedByEmail,
            alert.ResolutionNotes);

        return Result.Success(dto);
    }
}
