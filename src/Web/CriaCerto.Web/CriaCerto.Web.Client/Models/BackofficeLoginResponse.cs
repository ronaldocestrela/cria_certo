using CriaCerto.Modules.Backoffice.Application.Features.AdminUsers.Dtos;

namespace CriaCerto.Web.Client.Models;

public sealed class BackofficeLoginResponse
{
    public AdminAuthResultDto? AuthResult { get; init; }
    public bool MfaRequired { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ErrorCode { get; init; }

    public bool IsSuccess => AuthResult is not null && !string.IsNullOrEmpty(AuthResult.SessionToken);
}
