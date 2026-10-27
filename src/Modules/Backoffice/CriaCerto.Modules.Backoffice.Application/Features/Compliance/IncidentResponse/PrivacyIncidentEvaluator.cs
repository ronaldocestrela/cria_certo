using CriaCerto.BuildingBlocks.Abstractions.Results;

namespace CriaCerto.Modules.Backoffice.Application.Features.Compliance.IncidentResponse;

/// <summary>
/// Motor de avaliação e resposta técnica a incidentes de segurança com dados pessoais
/// em conformidade com o Art. 48 da LGPD, a Resolução CD/ANPD nº 15/2024 e o DPA B2B (ADR 0022).
/// </summary>
public sealed class PrivacyIncidentEvaluator
{
    private const string DefaultControllerName = "CriaCerto Bovino SaaS Ltda.";
    private const string DefaultControllerCnpj = "12.345.678/0001-90";
    private const string DefaultDpoName = "Encarregado pelo Tratamento de Dados Pessoais (DPO CriaCerto)";
    private const string DefaultDpoEmail = "dpo@criacerto.com.br";
    private const string DefaultDpoPortalUrl = "https://app.criacerto.com.br/dpo";

    /// <summary>
    /// Avalia a criticidade do incidente e determina a obrigatoriedade de comunicação
    /// à ANPD, aos titulares e/ou aos controladores B2B contratantes.
    /// </summary>
    public Result<SecurityIncidentAssessment> Evaluate(PrivacyIncidentEvaluationRequest request)
    {
        if (request is null)
        {
            return Result.Failure<SecurityIncidentAssessment>(
                Error.Validation("Compliance.InvalidIncidentRequest", "A requisição de avaliação de incidente é obrigatória."));
        }

        if (string.IsNullOrWhiteSpace(request.IncidentId))
        {
            return Result.Failure<SecurityIncidentAssessment>(
                Error.Validation("Compliance.IncidentIdRequired", "O identificador do incidente deve ser informado."));
        }

        // Critérios da Resolução CD/ANPD nº 15/2024 para Risco ou Dano Relevante:
        // 1. Severidade Alta ou Crítica
        // 2. Exposição de PII em texto claro com múltiplos titulares
        // 3. Exposição de credenciais de acesso ou dados financeiros
        var hasRelevantDamage = request.Severity is PrivacyIncidentSeverity.High or PrivacyIncidentSeverity.Critical
            || (request.IsPiiExposedInPlainText && request.ImpactedDataSubjectsCount > 0)
            || request.HasFinancialOrCredentialData;

        // Se dados estavam estritamente cifrados em repouso sem chave comprometida e sem risco aos titulares,
        // o Art. 48 desobriga notificação pública/externa caso não haja dano relevante.
        var requiresAnpdNotification = hasRelevantDamage && (request.Severity != PrivacyIncidentSeverity.Low || request.IsPiiExposedInPlainText);
        var requiresDataSubjectNotification = requiresAnpdNotification;

        // Sob o DPA B2B (ADR 0022), a CriaCerto atua como Operadora para dados de fazenda/vaqueiros/operadores.
        // Havendo incidente confirmado, o Controlador (Fazenda) deve ser notificado em até 24 horas.
        var requiresB2BControllerNotification = request.IsB2BOperatorData && hasRelevantDamage;

        DateTime? anpdDeadlineUtc = requiresAnpdNotification
            ? AddBusinessDays(request.DiscoveredAtUtc, 3)
            : null;

        DateTime? dataSubjectDeadlineUtc = requiresDataSubjectNotification
            ? AddBusinessDays(request.DiscoveredAtUtc, 3)
            : null;

        DateTime? b2bDeadlineUtc = requiresB2BControllerNotification
            ? request.DiscoveredAtUtc.AddHours(24)
            : null;

        DateTime? finalReportDeadlineUtc = requiresAnpdNotification && anpdDeadlineUtc.HasValue
            ? AddBusinessDays(anpdDeadlineUtc.Value, 20)
            : null;

        var recommendation = hasRelevantDamage
            ? $"Incidente com potencial dano relevante detectado. Comunicação obrigatória à ANPD e aos titulares no prazo de até 3 dias úteis ({anpdDeadlineUtc:yyyy-MM-dd HH:mm:ss} UTC). Mobilizar Comitê de Crise e acionar RUNBOOK-07."
            : "Incidente contido sem evidência de dano relevante aos titulares (dados protegidos por criptografia/blind index). Registrar ocorrência no Livro de Incidentes e ROPA interno (Art. 37 LGPD).";

        var assessment = new SecurityIncidentAssessment(
            IncidentId: request.IncidentId,
            DiscoveredAtUtc: request.DiscoveredAtUtc,
            Severity: request.Severity,
            RequiresAnpdNotification: requiresAnpdNotification,
            RequiresDataSubjectNotification: requiresDataSubjectNotification,
            RequiresB2BControllerNotification: requiresB2BControllerNotification,
            AnpdNotificationDeadlineUtc: anpdDeadlineUtc,
            DataSubjectNotificationDeadlineUtc: dataSubjectDeadlineUtc,
            B2BControllerNotificationDeadlineUtc: b2bDeadlineUtc,
            AnpdFinalReportDeadlineUtc: finalReportDeadlineUtc,
            AffectedTenantName: request.AffectedTenantName,
            Recommendation: recommendation
        );

        return Result.Success(assessment);
    }

    /// <summary>
    /// Gera o relatório preliminar canônico para notificação à ANPD conforme a Resolução CD/ANPD nº 15/2024.
    /// </summary>
    public Result<AnpdPreliminaryIncidentReportDto> GenerateAnpdPreliminaryReport(AnpdIncidentReportRequest request)
    {
        if (request is null)
        {
            return Result.Failure<AnpdPreliminaryIncidentReportDto>(
                Error.Validation("Compliance.InvalidReportRequest", "A requisição de relatório é obrigatória."));
        }

        if (string.IsNullOrWhiteSpace(request.IncidentProtocol))
        {
            return Result.Failure<AnpdPreliminaryIncidentReportDto>(
                Error.Validation("Compliance.ProtocolRequired", "O protocolo do incidente deve ser informado."));
        }

        var elapsedHours = (request.ContainedAtUtc - request.DiscoveredAtUtc).TotalHours;
        if (elapsedHours < 0)
        {
            elapsedHours = 0;
        }

        var conclusiveDeadlineUtc = AddBusinessDays(request.DiscoveredAtUtc, 20);

        var report = new AnpdPreliminaryIncidentReportDto(
            Protocol: request.IncidentProtocol,
            GeneratedAtUtc: DateTime.UtcNow,
            ControllerName: DefaultControllerName,
            ControllerCnpj: DefaultControllerCnpj,
            DpoName: DefaultDpoName,
            DpoEmail: DefaultDpoEmail,
            DpoPortalUrl: DefaultDpoPortalUrl,
            DiscoveredAtUtc: request.DiscoveredAtUtc,
            ContainedAtUtc: request.ContainedAtUtc,
            ElapsedHoursToContainment: elapsedHours,
            VulnerabilityVector: request.VulnerabilityVector,
            CompromisedCategories: request.CompromisedCategories ?? Array.Empty<string>(),
            ImpactedSubjectsCount: request.ImpactedSubjectsCount,
            PotentialImpacts: request.PotentialImpacts,
            ContainmentMeasures: request.ContainmentMeasures,
            DataSubjectCommunicationStrategy: request.DataSubjectCommunicationStrategy,
            ConclusiveReportDeadlineUtc: conclusiveDeadlineUtc
        );

        return Result.Success(report);
    }

    /// <summary>
    /// Adiciona dias úteis a uma data UTC, ignorando sábados e domingos.
    /// </summary>
    public static DateTime AddBusinessDays(DateTime startDateUtc, int businessDays)
    {
        var current = startDateUtc;
        var added = 0;
        while (added < businessDays)
        {
            current = current.AddDays(1);
            if (current.DayOfWeek != DayOfWeek.Saturday && current.DayOfWeek != DayOfWeek.Sunday)
            {
                added++;
            }
        }
        return current;
    }
}
