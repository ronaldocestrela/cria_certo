using MediatR;

namespace CriaCerto.Modules.Tenancy.Application.Events;

public sealed record TenantRegisteredIntegrationEvent(
    Guid TenantId,
    Guid? UserId,
    string? UserEmail,
    string? ReferralCode,
    string? OriginIpAddress,
    string? UserAgent,
    DateTime RegisteredAtUtc
) : INotification;
