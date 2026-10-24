using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace CriaCerto.Modules.Referral.Application.Telemetry;

public static class ReferralTelemetry
{
    public const string MeterName = "CriaCerto.Modules.Referral";
    public const string ActivitySourceName = "CriaCerto.Modules.Referral";
    public const string Version = "1.0.0";

    public static readonly Meter Meter = new(MeterName, Version);
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, Version);

    // Contadores e Histogramas
    public static readonly Counter<long> ClicksCounter = Meter.CreateCounter<long>(
        name: "referral.clicks.total",
        unit: "{click}",
        description: "Total de cliques registrados em links de indicação categorizados por canal e código");

    public static readonly Counter<long> AttributionsCounter = Meter.CreateCounter<long>(
        name: "referral.attributions.total",
        unit: "{attribution}",
        description: "Total de atribuições criadas ou convertidas categorizadas por canal e status");

    public static readonly Counter<long> RewardsCountCounter = Meter.CreateCounter<long>(
        name: "referral.rewards.total",
        unit: "{reward}",
        description: "Total de recompensas geradas categorizadas por tipo");

    public static readonly Counter<double> RewardsAmountCounter = Meter.CreateCounter<double>(
        name: "referral.rewards.amount",
        unit: "BRL",
        description: "Montante financeiro de recompensas concedidas em créditos ou comissões");

    public static readonly Histogram<double> OperationDurationHistogram = Meter.CreateHistogram<double>(
        name: "referral.operation_latency.duration_ms",
        unit: "ms",
        description: "Histograma de latência de processamento das operações do módulo de Referral");

    public static void RecordClick(string channel, string? code = null)
    {
        ClicksCounter.Add(1,
            new KeyValuePair<string, object?>("channel", channel),
            new KeyValuePair<string, object?>("code", code ?? "unknown"));
    }

    public static void RecordAttribution(string channel, string status)
    {
        AttributionsCounter.Add(1,
            new KeyValuePair<string, object?>("channel", channel),
            new KeyValuePair<string, object?>("status", status));
    }

    public static void RecordReward(string rewardType, double amount, string currency = "BRL")
    {
        RewardsCountCounter.Add(1,
            new KeyValuePair<string, object?>("reward_type", rewardType));

        RewardsAmountCounter.Add(amount,
            new KeyValuePair<string, object?>("reward_type", rewardType),
            new KeyValuePair<string, object?>("currency", currency));
    }
}
