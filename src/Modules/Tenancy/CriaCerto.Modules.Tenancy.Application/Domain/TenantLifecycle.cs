namespace CriaCerto.Modules.Tenancy.Application.Domain;

public static class TenantLifecycle
{
    private static readonly HashSet<(TenantStatus From, TenantStatus To)> AllowedTransitions =
    [
        (TenantStatus.Trial, TenantStatus.Active),
        (TenantStatus.Trial, TenantStatus.Suspended),
        (TenantStatus.Trial, TenantStatus.Cancelled),
        (TenantStatus.Active, TenantStatus.PastDue),
        (TenantStatus.Active, TenantStatus.Suspended),
        (TenantStatus.Active, TenantStatus.Cancelled),
        (TenantStatus.PastDue, TenantStatus.Active),
        (TenantStatus.PastDue, TenantStatus.Suspended),
        (TenantStatus.PastDue, TenantStatus.Cancelled),
        (TenantStatus.Suspended, TenantStatus.Active),
        (TenantStatus.Suspended, TenantStatus.Cancelled),
        (TenantStatus.Cancelled, TenantStatus.Archived)
    ];

    private static readonly HashSet<TenantStatus> RestrictedWhenProtected =
    [
        TenantStatus.Suspended,
        TenantStatus.Cancelled,
        TenantStatus.Archived
    ];

    public const int MinJustificationLength = 15;

    public static bool CanTransition(TenantStatus from, TenantStatus to) =>
        AllowedTransitions.Contains((from, to));

    public static bool IsRestrictedWhenProtected(TenantStatus target) =>
        RestrictedWhenProtected.Contains(target);

    public static bool CanProducerAccess(TenantStatus status) =>
        status is TenantStatus.Trial or TenantStatus.Active or TenantStatus.PastDue;

    public static bool CanProducerAccess(string status) =>
        TryParseStatus(status, out var parsed) && CanProducerAccess(parsed);

    public static string ToStatusString(TenantStatus status) => status.ToString();

    public static bool TryParseStatus(string? status, out TenantStatus parsed)
    {
        parsed = default;
        if (string.IsNullOrWhiteSpace(status))
        {
            return false;
        }

        if (string.Equals(status, "Maintenance", StringComparison.OrdinalIgnoreCase))
        {
            parsed = TenantStatus.Suspended;
            return true;
        }

        return Enum.TryParse(status, ignoreCase: true, out parsed);
    }

    public static TenantStatus ParseStatus(string status)
    {
        if (TryParseStatus(status, out var parsed))
        {
            return parsed;
        }

        throw new ArgumentException($"Status de tenant inválido: '{status}'.", nameof(status));
    }
}
