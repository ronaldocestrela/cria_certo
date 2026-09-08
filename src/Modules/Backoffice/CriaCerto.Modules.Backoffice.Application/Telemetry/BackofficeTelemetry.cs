using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace CriaCerto.Modules.Backoffice.Application.Telemetry;

public static class BackofficeTelemetry
{
    public const string MeterName = "CriaCerto.Modules.Backoffice";
    public const string ActivitySourceName = "CriaCerto.Modules.Backoffice";
    public const string Version = "1.0.0";

    public static readonly Meter Meter = new(MeterName, Version);
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, Version);

    // Metrics Instruments
    public static readonly Counter<long> AdminActionsCounter = Meter.CreateCounter<long>(
        name: "backoffice.admin_actions.total",
        unit: "{action}",
        description: "Total de ações administrativas executadas categorizadas por categoria, severidade e papel");

    public static readonly Counter<long> PolicyFailuresCounter = Meter.CreateCounter<long>(
        name: "backoffice.policy_failures.total",
        unit: "{failure}",
        description: "Total de falhas de autorização e negações de política no backoffice");

    public static readonly UpDownCounter<long> ActiveImpersonationGauge = Meter.CreateUpDownCounter<long>(
        name: "backoffice.impersonation_sessions.active",
        unit: "{session}",
        description: "Quantidade atual de sessões ativas de impersonação monitoradas");

    public static readonly Histogram<double> OperationDurationHistogram = Meter.CreateHistogram<double>(
        name: "backoffice.operation_latency.duration_ms",
        unit: "ms",
        description: "Histograma de latência de processamento das operações e consultas do Backoffice");

    public static readonly Counter<long> AlertsTriggeredCounter = Meter.CreateCounter<long>(
        name: "backoffice.alerts.triggered.total",
        unit: "{alert}",
        description: "Total de alertas operacionais disparados categorizados por severidade e regra");

    public static void RecordAction(string category, string severity, string actorRole)
    {
        AdminActionsCounter.Add(1,
            new KeyValuePair<string, object?>("category", category),
            new KeyValuePair<string, object?>("severity", severity),
            new KeyValuePair<string, object?>("actor_role", actorRole));
    }

    public static void RecordPolicyFailure(string reason, string path, string? actorEmail = null)
    {
        PolicyFailuresCounter.Add(1,
            new KeyValuePair<string, object?>("reason", reason),
            new KeyValuePair<string, object?>("path", path),
            new KeyValuePair<string, object?>("actor", actorEmail ?? "anonymous"));
    }

    public static void RecordAlert(string ruleCode, string severity)
    {
        AlertsTriggeredCounter.Add(1,
            new KeyValuePair<string, object?>("rule_code", ruleCode),
            new KeyValuePair<string, object?>("severity", severity));
    }
}
