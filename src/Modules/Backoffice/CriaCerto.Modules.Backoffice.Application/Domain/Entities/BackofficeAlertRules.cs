using CriaCerto.Modules.Backoffice.Application.Domain.Enums;

namespace CriaCerto.Modules.Backoffice.Application.Domain.Entities;

public static class BackofficeAlertRules
{
    public const string PolicyBruteForce = "ALR_POLICY_BRUTE_FORCE";
    public const string OffHoursCriticalAction = "ALR_OFF_HOURS_CRITICAL_ACTION";
    public const string ImpersonationBurst = "ALR_IMPERSONATION_BURST";
    public const string ForensicTamperDetected = "ALR_FORENSIC_TAMPER_DETECTED";
    public const string SimulatedAlert = "ALR_SIMULATED_ALERT";

    public static string GetDefaultTitle(string ruleCode) => ruleCode switch
    {
        PolicyBruteForce => "Múltiplas Violações Consecutivas de Acesso / Política",
        OffHoursCriticalAction => "Operação Crítica Administrativa Fora da Janela Regular",
        ImpersonationBurst => "Surto Anômalo de Sessões de Impersonação",
        ForensicTamperDetected => "Violação Crítica de Integridade na Trilha Forense",
        SimulatedAlert => "Alerta Operacional Simulado para Validação",
        _ => "Alerta Operacional do Backoffice"
    };

    public static AlertSeverity GetDefaultSeverity(string ruleCode) => ruleCode switch
    {
        PolicyBruteForce => AlertSeverity.Warning,
        OffHoursCriticalAction => AlertSeverity.Warning,
        ImpersonationBurst => AlertSeverity.Warning,
        ForensicTamperDetected => AlertSeverity.Critical,
        SimulatedAlert => AlertSeverity.Info,
        _ => AlertSeverity.Info
    };
}
