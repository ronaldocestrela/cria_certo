using System.Text.Json;
using System.Text.RegularExpressions;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Features.Impersonation.Dtos;
using CriaCerto.Modules.Backoffice.Application.Security;
using CriaCerto.Modules.Tenancy.Application.Features.BackofficeTenants;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CriaCerto.Modules.Backoffice.Application.Features.Impersonation.Commands;

public record StartImpersonationSessionCommand(
    Guid TargetTenantId,
    Guid? TargetUserId,
    string SupportTicket,
    string Justification,
    int DurationMinutes,
    Guid AdminUserId,
    string AdminUserEmail,
    string IpAddress,
    string UserAgent) : IRequest<Result<ImpersonationSessionDto>>;

public class StartImpersonationSessionCommandHandler : IRequestHandler<StartImpersonationSessionCommand, Result<ImpersonationSessionDto>>
{
    private static readonly Regex TicketRegex = new(@"^[A-Za-z0-9\-_#]{3,30}$", RegexOptions.Compiled);
    private readonly ISender _sender;
    private readonly DbContext _dbContext;
    private readonly IBackofficeTokenService _tokenService;

    public StartImpersonationSessionCommandHandler(
        ISender sender,
        DbContext dbContext,
        IBackofficeTokenService tokenService)
    {
        _sender = sender;
        _dbContext = dbContext;
        _tokenService = tokenService;
    }

    public async Task<Result<ImpersonationSessionDto>> Handle(
        StartImpersonationSessionCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Double Safeguard - Validation of Ticket and Justification
        if (string.IsNullOrWhiteSpace(command.SupportTicket))
        {
            return Result.Failure<ImpersonationSessionDto>(
                Error.Validation("Impersonation.TicketRequired", "O número/código do chamado de suporte é obrigatório para iniciar a impersonação."));
        }

        var trimmedTicket = command.SupportTicket.Trim();
        if (!TicketRegex.IsMatch(trimmedTicket))
        {
            return Result.Failure<ImpersonationSessionDto>(
                Error.Validation("Impersonation.InvalidTicketFormat", "O formato do ticket de suporte é inválido. Utilize um identificador alfanumérico válido (Ex: SUP-1234)."));
        }

        if (string.IsNullOrWhiteSpace(command.Justification) || command.Justification.Trim().Length < 10)
        {
            return Result.Failure<ImpersonationSessionDto>(
                Error.Validation("Impersonation.JustificationRequired", "A justificativa operacional é obrigatória e deve conter no mínimo 10 caracteres."));
        }

        // 2. Double Safeguard - Tenant Eligibility and Sensitivity Safeguards
        var tenantResult = await _sender.Send(new GetTenantBackofficeDetailQuery(command.TargetTenantId), cancellationToken);
        if (tenantResult.IsFailure || tenantResult.Value is null)
        {
            return Result.Failure<ImpersonationSessionDto>(
                Error.NotFound("Impersonation.TenantNotFound", "O tenant solicitado não foi encontrado."));
        }

        var tenant = tenantResult.Value;

        if (tenant.Status is "Suspended" or "Cancelled" or "Archived")
        {
            return Result.Failure<ImpersonationSessionDto>(
                Error.Conflict("Impersonation.TenantBlocked", $"Não é permitido iniciar sessão de impersonação em tenant com status '{tenant.Status}'."));
        }

        if (tenant.IsProtected)
        {
            return Result.Failure<ImpersonationSessionDto>(
                Error.Conflict("Impersonation.TenantProtected", "Este tenant possui proteção de salvaguarda ativa e não permite impersonação direta."));
        }

        // 3. Clean up any previous active sessions for this admin
        var existingActiveSessions = await _dbContext.Set<ImpersonationSession>()
            .Where(s => s.AdminUserId == command.AdminUserId && s.Status == ImpersonationSessionStatus.Active)
            .ToListAsync(cancellationToken);

        foreach (var existing in existingActiveSessions)
        {
            existing.Revoke("Substituída por nova sessão de impersonação.");
        }

        // 4. Create Impersonation Session
        var session = ImpersonationSession.Create(
            command.AdminUserId,
            command.AdminUserEmail,
            tenant.Id,
            tenant.Name,
            command.TargetUserId,
            tenant.TechnicalOwnerEmail ?? tenant.CommercialOwnerEmail,
            trimmedTicket,
            command.Justification.Trim(),
            command.DurationMinutes,
            command.IpAddress,
            command.UserAgent);

        _dbContext.Set<ImpersonationSession>().Add(session);

        // 5. Generate Ephemeral Impersonation JWT
        var token = _tokenService.GenerateImpersonationToken(
            command.AdminUserId,
            command.AdminUserEmail,
            tenant.Id,
            tenant.Name,
            command.TargetUserId,
            session.TargetUserEmail,
            session.Id,
            trimmedTicket,
            TimeSpan.FromMinutes(session.DurationMinutes));

        // 6. Write to AuditLog
        var auditDetails = JsonSerializer.Serialize(new
        {
            SessionId = session.Id,
            TargetTenantId = tenant.Id,
            TargetTenantName = tenant.Name,
            TargetUserId = command.TargetUserId,
            SupportTicket = trimmedTicket,
            Justification = command.Justification.Trim(),
            DurationMinutes = session.DurationMinutes,
            ExpiresAtUtc = session.ExpiresAtUtc,
            UserAgent = command.UserAgent
        });

        var auditLog = AuditLog.Create(
            command.AdminUserId,
            command.AdminUserEmail,
            "Impersonation.Started",
            $"Tenant/{tenant.Id}",
            command.IpAddress,
            auditDetails);

        _dbContext.Set<AuditLog>().Add(auditLog);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var dto = new ImpersonationSessionDto(
            session.Id,
            token,
            session.TargetTenantId,
            session.TargetTenantName,
            session.TargetUserId,
            session.TargetUserEmail,
            session.SupportTicket,
            session.Justification,
            session.StartedAtUtc,
            session.ExpiresAtUtc,
            session.GetRemainingSeconds(),
            session.Status.ToString());

        return Result.Success(dto);
    }
}
