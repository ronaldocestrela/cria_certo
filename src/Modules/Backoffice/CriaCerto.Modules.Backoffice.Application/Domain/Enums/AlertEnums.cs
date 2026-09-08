namespace CriaCerto.Modules.Backoffice.Application.Domain.Enums;

public enum AlertSeverity
{
    Info = 1,
    Warning = 2,
    Critical = 3
}

public enum AlertStatus
{
    Active = 1,
    Acknowledged = 2,
    Resolved = 3
}

public enum OperationalHealthStatus
{
    Healthy = 1,
    Degraded = 2,
    Critical = 3
}
