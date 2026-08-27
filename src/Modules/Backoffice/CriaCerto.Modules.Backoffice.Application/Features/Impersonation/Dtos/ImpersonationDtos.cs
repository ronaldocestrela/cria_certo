namespace CriaCerto.Modules.Backoffice.Application.Features.Impersonation.Dtos;

public record ImpersonationSessionDto(
    Guid SessionId,
    string Token,
    Guid TargetTenantId,
    string TargetTenantName,
    Guid? TargetUserId,
    string? TargetUserEmail,
    string SupportTicket,
    string Justification,
    DateTime StartedAtUtc,
    DateTime ExpiresAtUtc,
    int RemainingSeconds,
    string Status);

public record ImpersonationAuditItemDto(
    Guid Id,
    Guid AdminUserId,
    string AdminUserEmail,
    Guid TargetTenantId,
    string TargetTenantName,
    Guid? TargetUserId,
    string? TargetUserEmail,
    string SupportTicket,
    string Justification,
    int DurationMinutes,
    DateTime StartedAtUtc,
    DateTime ExpiresAtUtc,
    DateTime? EndedAtUtc,
    string Status,
    string IpAddress,
    string? RevocationReason);

public record StartImpersonationRequest(
    Guid TargetTenantId,
    Guid? TargetUserId,
    string SupportTicket,
    string Justification,
    int DurationMinutes = 15);

public record StopImpersonationRequest(
    Guid SessionId,
    string? Reason = null);
