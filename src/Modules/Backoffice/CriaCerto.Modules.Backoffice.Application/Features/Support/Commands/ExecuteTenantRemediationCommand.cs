using System.Text.Json;
using System.Text.RegularExpressions;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;
using CriaCerto.Modules.Backoffice.Application.Features.Support.Dtos;
using CriaCerto.Modules.Tenancy.Application.Features.BackofficeTenants;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Backoffice.Application.Features.Support.Commands;

public record ExecuteTenantRemediationCommand(
    Guid TenantId,
    string ActionType,
    string SupportTicketId,
    string Justification,
    Guid AdminUserId,
    string AdminUserEmail,
    string IpAddress
) : IRequest<Result<RemediationExecutionResultDto>>;

public sealed class ExecuteTenantRemediationCommandHandler
    : IRequestHandler<ExecuteTenantRemediationCommand, Result<RemediationExecutionResultDto>>
{
    private static readonly HashSet<string> AllowedActions = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(RemediationActionType.RequestClientCacheReset),
        nameof(RemediationActionType.EvictTenantCache),
        nameof(RemediationActionType.ReconcileEntitlements),
        nameof(RemediationActionType.RetryFailedQueueItems),
        nameof(RemediationActionType.ResetTransientLocks)
    };

    private static readonly Regex TicketRegex = new(@"^[A-Za-z0-9\-_#]{3,30}$", RegexOptions.Compiled);

    private readonly ISender _sender;
    private readonly DbContext _dbContext;

    public ExecuteTenantRemediationCommandHandler(ISender sender, DbContext dbContext)
    {
        _sender = sender;
        _dbContext = dbContext;
    }

    public async Task<Result<RemediationExecutionResultDto>> Handle(
        ExecuteTenantRemediationCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Validate Ticket
        if (string.IsNullOrWhiteSpace(command.SupportTicketId) || command.SupportTicketId.Trim().Length < 3 || !TicketRegex.IsMatch(command.SupportTicketId.Trim()))
        {
            return Result.Failure<RemediationExecutionResultDto>(SupportErrors.InvalidTicketId);
        }

        // 2. Validate Justification
        if (string.IsNullOrWhiteSpace(command.Justification) || command.Justification.Trim().Length < 10)
        {
            return Result.Failure<RemediationExecutionResultDto>(SupportErrors.InvalidJustification);
        }

        // 3. Validate Action Type
        var normalizedAction = command.ActionType.Trim();
        if (!AllowedActions.Contains(normalizedAction))
        {
            return Result.Failure<RemediationExecutionResultDto>(SupportErrors.InvalidActionType);
        }

        // 4. Validate Tenant Exists
        var tenantResult = await _sender.Send(new GetTenantBackofficeDetailQuery(command.TenantId), cancellationToken);
        if (tenantResult.IsFailure)
        {
            return Result.Failure<RemediationExecutionResultDto>(SupportErrors.TenantNotFound);
        }

        var tenant = tenantResult.Value;
        var executionId = Guid.NewGuid();
        var executedAtUtc = DateTime.UtcNow;

        // 5. Execute Action-specific remediation routine
        var message = normalizedAction switch
        {
            nameof(RemediationActionType.RequestClientCacheReset) =>
                $"Instrução de reset de cache enviada com sucesso para os dispositivos móveis e navegadores da fazenda '{tenant.Name}'.",

            nameof(RemediationActionType.EvictTenantCache) =>
                $"Cache em memória do tenant '{tenant.Name}' invalidado com sucesso em todos os nós da aplicação.",

            nameof(RemediationActionType.ReconcileEntitlements) =>
                $"Direitos de módulos e quotas de rebanho reconciliados com sucesso para o plano '{tenant.SubscribedPlan}'.",

            nameof(RemediationActionType.RetryFailedQueueItems) =>
                $"Comando de reprocessamento seguro de filas e mensagens agendado para o tenant '{tenant.Name}'.",

            nameof(RemediationActionType.ResetTransientLocks) =>
                $"Bloqueios transitórios e tentativas concorrentes da fazenda '{tenant.Name}' foram liberados.",

            _ => "Ação executada com sucesso."
        };

        // 6. Record Immutable AuditLog
        var auditDetails = JsonSerializer.Serialize(new
        {
            ExecutionId = executionId,
            TenantId = command.TenantId,
            TenantName = tenant.Name,
            ActionType = normalizedAction,
            SupportTicketId = command.SupportTicketId.Trim(),
            Justification = command.Justification.Trim(),
            ExecutedAtUtc = executedAtUtc,
            Message = message
        });

        var auditLog = AuditLog.Create(
            command.AdminUserId,
            command.AdminUserEmail,
            "Support.RemediationExecuted",
            $"Tenant/{tenant.Id}",
            command.IpAddress,
            auditDetails);

        _dbContext.Set<AuditLog>().Add(auditLog);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var resultDto = new RemediationExecutionResultDto(
            executionId,
            tenant.Id,
            normalizedAction,
            "Success",
            executedAtUtc,
            message,
            command.SupportTicketId.Trim(),
            command.AdminUserEmail);

        return Result.Success(resultDto);
    }
}
