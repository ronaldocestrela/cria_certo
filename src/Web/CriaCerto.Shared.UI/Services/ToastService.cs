using CriaCerto.Shared.UI.Models;

namespace CriaCerto.Shared.UI.Services;

public class ToastService : IToastService
{
    private readonly List<ToastMessage> _activeToasts = new();

    public event Action<ToastMessage>? OnShow;
    public event Action<Guid>? OnHide;

    public IReadOnlyList<ToastMessage> ActiveToasts
    {
        get
        {
            lock (_activeToasts)
            {
                return _activeToasts.ToList().AsReadOnly();
            }
        }
    }

    public void ShowSuccess(string message, string? title = null, int durationMs = 5000)
    {
        AddToast(title ?? "Sucesso", message, ToastLevel.Success, durationMs);
    }

    public void ShowError(string message, string? title = null, int durationMs = 7000)
    {
        AddToast(title ?? "Erro", message, ToastLevel.Error, durationMs);
    }

    public void ShowWarning(string message, string? title = null, int durationMs = 6000)
    {
        AddToast(title ?? "Atenção", message, ToastLevel.Warning, durationMs);
    }

    public void ShowInfo(string message, string? title = null, int durationMs = 5000)
    {
        AddToast(title ?? "Informação", message, ToastLevel.Info, durationMs);
    }

    public void RemoveToast(Guid id)
    {
        bool removed = false;
        lock (_activeToasts)
        {
            var existing = _activeToasts.FirstOrDefault(t => t.Id == id);
            if (existing != null)
            {
                _activeToasts.Remove(existing);
                removed = true;
            }
        }

        if (removed)
        {
            OnHide?.Invoke(id);
        }
    }

    private void AddToast(string title, string message, ToastLevel level, int durationMs)
    {
        var toast = new ToastMessage(
            Id: Guid.NewGuid(),
            Title: title,
            Message: message,
            Level: level,
            Timestamp: DateTime.UtcNow,
            DurationMs: durationMs);

        lock (_activeToasts)
        {
            _activeToasts.Add(toast);
        }

        OnShow?.Invoke(toast);

        if (durationMs > 0)
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(durationMs);
                RemoveToast(toast.Id);
            });
        }
    }
}
