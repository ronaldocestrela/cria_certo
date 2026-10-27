namespace CriaCerto.Modules.Backoffice.Application.Features.Compliance.IncidentResponse;

/// <summary>
/// Solicitação de avaliação técnica de incidente de segurança com dados pessoais.
/// </summary>
public sealed record PrivacyIncidentEvaluationRequest(
    string IncidentId,
    string Title,
    DateTime DiscoveredAtUtc,
    PrivacyIncidentSeverity Severity,
    bool IsPiiExposedInPlainText,
    int ImpactedDataSubjectsCount,
    bool HasFinancialOrCredentialData,
    bool IsB2BOperatorData,
    Guid? AffectedTenantId,
    string? AffectedTenantName
);

/// <summary>
/// Parecer técnico-jurídico preliminar da avaliação do incidente pelo DPO e Segurança.
/// </summary>
public sealed record SecurityIncidentAssessment(
    string IncidentId,
    DateTime DiscoveredAtUtc,
    PrivacyIncidentSeverity Severity,
    bool RequiresAnpdNotification,
    bool RequiresDataSubjectNotification,
    bool RequiresB2BControllerNotification,
    DateTime? AnpdNotificationDeadlineUtc,
    DateTime? DataSubjectNotificationDeadlineUtc,
    DateTime? B2BControllerNotificationDeadlineUtc,
    DateTime? AnpdFinalReportDeadlineUtc,
    string? AffectedTenantName,
    string Recommendation
);

/// <summary>
/// Solicitação para emissão do relatório preliminar canônico de notificação de incidente à ANPD.
/// </summary>
public sealed record AnpdIncidentReportRequest(
    string IncidentProtocol,
    DateTime DiscoveredAtUtc,
    DateTime ContainedAtUtc,
    string VulnerabilityVector,
    IReadOnlyList<string> CompromisedCategories,
    int ImpactedSubjectsCount,
    string PotentialImpacts,
    string ContainmentMeasures,
    string DataSubjectCommunicationStrategy
);

/// <summary>
/// Estrutura canônica do Relatório Preliminar de Notificação de Incidente à ANPD (Resolução CD/ANPD nº 15/2024).
/// </summary>
public sealed record AnpdPreliminaryIncidentReportDto(
    string Protocol,
    DateTime GeneratedAtUtc,
    string ControllerName,
    string ControllerCnpj,
    string DpoName,
    string DpoEmail,
    string DpoPortalUrl,
    DateTime DiscoveredAtUtc,
    DateTime ContainedAtUtc,
    double ElapsedHoursToContainment,
    string VulnerabilityVector,
    IReadOnlyList<string> CompromisedCategories,
    int ImpactedSubjectsCount,
    string PotentialImpacts,
    string ContainmentMeasures,
    string DataSubjectCommunicationStrategy,
    DateTime ConclusiveReportDeadlineUtc
)
{
    public string RenderMarkdownSummary()
    {
        return $"""
        # RELATÓRIO PRELIMINAR DE INCIDENTE DE SEGURANÇA (ART. 48 LGPD)
        **Protocolo**: {Protocol}
        **Data de Emissão**: {GeneratedAtUtc:yyyy-MM-dd HH:mm:ss} UTC

        ---

        ## 1. Identificação do Controlador e Encarregado (DPO)
        * **Controlador**: {ControllerName} (CNPJ: {ControllerCnpj})
        * **Encarregado de Proteção de Dados (DPO)**: {DpoName}
        * **E-mail de Contato**: {DpoEmail}
        * **Canal Oficial de Atendimento**: {DpoPortalUrl}

        ---

        ## 2. Cronologia e Detecção
        * **Momento da Ciência Inequívoca (T0)**: {DiscoveredAtUtc:yyyy-MM-dd HH:mm:ss} UTC
        * **Momento da Contenção**: {ContainedAtUtc:yyyy-MM-dd HH:mm:ss} UTC
        * **Tempo Decorrido para Contenção**: {ElapsedHoursToContainment:F2} horas

        ---

        ## 3. Descrição do Incidente e Categorias de Dados
        * **Vetor de Vulnerabilidade / Causa**: {VulnerabilityVector}
        * **Categorias de Dados Comprometidas**: {string.Join(", ", CompromisedCategories)}
        * **Estimativa de Titulares Afetados**: {ImpactedSubjectsCount}

        ---

        ## 4. Avaliação de Riscos e Impactos aos Titulares
        {PotentialImpacts}

        ---

        ## 5. Medidas Técnicas de Segurança e Mitigação Adotadas
        {ContainmentMeasures}

        ---

        ## 6. Comunicação aos Titulares de Dados
        {DataSubjectCommunicationStrategy}

        ---

        ## 7. Prazo para Apresentação do Relatório Conclusivo à ANPD
        * **Prazo Legal Estipulado**: até {ConclusiveReportDeadlineUtc:yyyy-MM-dd HH:mm:ss} UTC (Resolução CD/ANPD nº 15/2024).
        """;
    }
}
