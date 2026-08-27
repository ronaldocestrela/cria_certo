using System.Text.Json;
using CriaCerto.Modules.Backoffice.Application.Features.Impersonation.Dtos;
using CriaCerto.Web.Client.Auth;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace CriaCerto.Web.Client.Services;

public class ImpersonationStateService : IImpersonationStateService, IDisposable
{
    private const string BackupTokenKey = "adminBackupToken";
    private const string ImpersonationSessionKey = "impersonationSession";

    private readonly IJSRuntime _jsRuntime;
    private readonly IBackofficeApiClient _apiClient;
    private readonly CustomAuthStateProvider _authStateProvider;
    private readonly NavigationManager _navigation;
    private readonly IToastService _toastService;

    private PeriodicTimer? _timer;
    private CancellationTokenSource? _timerCts;

    public bool IsImpersonating { get; private set; }
    public ImpersonationSessionDto? CurrentSession { get; private set; }
    public int RemainingSeconds { get; private set; }
    public bool IsLoading { get; private set; }

    public string FormattedRemainingTime
    {
        get
        {
            if (RemainingSeconds <= 0) return "00:00";
            var minutes = RemainingSeconds / 60;
            var seconds = RemainingSeconds % 60;
            return $"{minutes:D2}:{seconds:D2}";
        }
    }

    public event Action? OnSessionChanged;
    public event Action? OnTimerTick;

    public ImpersonationStateService(
        IJSRuntime jsRuntime,
        IBackofficeApiClient apiClient,
        CustomAuthStateProvider authStateProvider,
        NavigationManager navigation,
        IToastService toastService)
    {
        _jsRuntime = jsRuntime;
        _apiClient = apiClient;
        _authStateProvider = authStateProvider;
        _navigation = navigation;
        _toastService = toastService;
    }

    public async Task InitializeAsync()
    {
        if (!OperatingSystem.IsBrowser()) return;

        try
        {
            var sessionJson = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", ImpersonationSessionKey);
            if (!string.IsNullOrWhiteSpace(sessionJson))
            {
                var session = JsonSerializer.Deserialize<ImpersonationSessionDto>(sessionJson);
                if (session != null)
                {
                    var seconds = (int)(session.ExpiresAtUtc - DateTime.UtcNow).TotalSeconds;
                    if (seconds > 0)
                    {
                        CurrentSession = session;
                        IsImpersonating = true;
                        RemainingSeconds = seconds;
                        StartCountdown();
                        OnSessionChanged?.Invoke();
                        return;
                    }
                }
            }

            // If session expired or invalid, clean up gracefully
            if (IsImpersonating)
            {
                await StopSessionAsync("Sessão expirada.");
            }
        }
        catch
        {
            // Ignore init storage errors
        }
    }

    public async Task<bool> StartSessionAsync(ImpersonationSessionDto session)
    {
        IsLoading = true;
        try
        {
            // 1. Backup current admin auth token
            var currentToken = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "authToken");
            if (!string.IsNullOrWhiteSpace(currentToken))
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", BackupTokenKey, currentToken);
            }

            // 2. Set impersonation session
            CurrentSession = session;
            IsImpersonating = true;
            RemainingSeconds = session.RemainingSeconds > 0 ? session.RemainingSeconds : (int)(session.ExpiresAtUtc - DateTime.UtcNow).TotalSeconds;
            if (RemainingSeconds <= 0) RemainingSeconds = 15 * 60;

            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", ImpersonationSessionKey, JsonSerializer.Serialize(session));

            // 3. Mark user authenticated with ephemeral impersonation token
            await _authStateProvider.MarkUserAsAuthenticated(session.Token);

            StartCountdown();
            OnSessionChanged?.Invoke();

            _toastService.ShowSuccess($"Impersonação iniciada com sucesso no tenant {session.TargetTenantName}.");

            // 4. Navigate to main client dashboard or registry
            _navigation.NavigateTo("/breeding/registry");

            return true;
        }
        catch (Exception ex)
        {
            _toastService.ShowError($"Falha ao iniciar impersonação: {ex.Message}");
            return false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task<bool> StopSessionAsync(string? reason = null)
    {
        IsLoading = true;
        StopCountdown();

        try
        {
            if (CurrentSession != null)
            {
                await _apiClient.StopImpersonationAsync(CurrentSession.SessionId, reason);
            }

            // 1. Restore backed up admin token
            var backupToken = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", BackupTokenKey);
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", ImpersonationSessionKey);
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", BackupTokenKey);

            CurrentSession = null;
            IsImpersonating = false;
            RemainingSeconds = 0;

            if (!string.IsNullOrWhiteSpace(backupToken))
            {
                await _authStateProvider.MarkUserAsAuthenticated(backupToken);
            }
            else
            {
                await _authStateProvider.MarkUserAsLoggedOut();
            }

            OnSessionChanged?.Invoke();

            _toastService.ShowInfo(string.IsNullOrWhiteSpace(reason)
                ? "Sessão de impersonação encerrada com sucesso."
                : $"Sessão de impersonação encerrada: {reason}");

            _navigation.NavigateTo("/backoffice/tenants");

            return true;
        }
        catch (Exception ex)
        {
            _toastService.ShowError($"Erro ao encerrar sessão: {ex.Message}");
            return false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void StartCountdown()
    {
        StopCountdown();
        _timerCts = new CancellationTokenSource();
        _timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        _ = Task.Run(async () =>
        {
            try
            {
                while (_timer != null && await _timer.WaitForNextTickAsync(_timerCts.Token))
                {
                    if (RemainingSeconds > 0)
                    {
                        RemainingSeconds--;
                        OnTimerTick?.Invoke();
                    }
                    else
                    {
                        await StopSessionAsync("O tempo limite (TTL) da sessão de impersonação expirou.");
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Timer cancelled normally
            }
        });
    }

    private void StopCountdown()
    {
        _timerCts?.Cancel();
        _timerCts?.Dispose();
        _timerCts = null;

        _timer?.Dispose();
        _timer = null;
    }

    public void Dispose()
    {
        StopCountdown();
    }
}
