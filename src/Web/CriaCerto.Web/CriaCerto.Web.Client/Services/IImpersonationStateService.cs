using CriaCerto.Modules.Backoffice.Application.Features.Impersonation.Dtos;

namespace CriaCerto.Web.Client.Services;

public interface IImpersonationStateService
{
    bool IsImpersonating { get; }
    ImpersonationSessionDto? CurrentSession { get; }
    int RemainingSeconds { get; }
    string FormattedRemainingTime { get; }
    bool IsLoading { get; }

    event Action? OnSessionChanged;
    event Action? OnTimerTick;

    Task InitializeAsync();
    Task<bool> StartSessionAsync(ImpersonationSessionDto session);
    Task<bool> StopSessionAsync(string? reason = null);
}
