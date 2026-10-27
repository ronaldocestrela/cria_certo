using CriaCerto.Modules.Backoffice.Application.Features.Compliance.IncidentResponse;
using FluentAssertions;
using Xunit;

namespace CriaCerto.Modules.Backoffice.UnitTests.Features;

public class PrivacyIncidentEvaluatorTests
{
    private readonly PrivacyIncidentEvaluator _evaluator = new();

    [Fact]
    public void EvaluateIncident_WhenLowSeverityAndEncrypted_ShouldNotRequireExternalNotification()
    {
        // Arrange: Backup ou registro cifrado com AES-256-GCM sem comprometimento de chave
        var discoveredAt = new DateTime(2026, 10, 26, 10, 0, 0, DateTimeKind.Utc); // Segunda-feira
        var request = new PrivacyIncidentEvaluationRequest(
            IncidentId: "INC-TEST-001",
            Title: "Tentativa isolada de exfiltração contida por WAF",
            DiscoveredAtUtc: discoveredAt,
            Severity: PrivacyIncidentSeverity.Low,
            IsPiiExposedInPlainText: false,
            ImpactedDataSubjectsCount: 0,
            HasFinancialOrCredentialData: false,
            IsB2BOperatorData: false,
            AffectedTenantId: null,
            AffectedTenantName: null
        );

        // Act
        var result = _evaluator.Evaluate(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var assessment = result.Value;
        assessment.RequiresAnpdNotification.Should().BeFalse();
        assessment.RequiresDataSubjectNotification.Should().BeFalse();
        assessment.RequiresB2BControllerNotification.Should().BeFalse();
        assessment.Recommendation.Should().Contain("Livro de Incidentes");
    }

    [Fact]
    public void EvaluateIncident_WhenDiscoveredOnFriday_ShouldCalculateThreeBusinessDaysAccurately()
    {
        // Arrange: Descoberta na Sexta-feira, 30/10/2026
        var fridayUtc = new DateTime(2026, 10, 30, 14, 0, 0, DateTimeKind.Utc);
        var request = new PrivacyIncidentEvaluationRequest(
            IncidentId: "INC-TEST-002",
            Title: "Vazamento acidental de base cadastral de produtores",
            DiscoveredAtUtc: fridayUtc,
            Severity: PrivacyIncidentSeverity.High,
            IsPiiExposedInPlainText: true,
            ImpactedDataSubjectsCount: 150,
            HasFinancialOrCredentialData: false,
            IsB2BOperatorData: false,
            AffectedTenantId: null,
            AffectedTenantName: null
        );

        // Act
        var result = _evaluator.Evaluate(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var assessment = result.Value;
        assessment.RequiresAnpdNotification.Should().BeTrue();
        assessment.RequiresDataSubjectNotification.Should().BeTrue();

        // 3 dias úteis a contar de sexta-feira:
        // Dia 1: Segunda 02/11/2026
        // Dia 2: Terça 03/11/2026
        // Dia 3: Quarta 04/11/2026
        var expectedDeadlineUtc = new DateTime(2026, 11, 4, 14, 0, 0, DateTimeKind.Utc);
        assessment.AnpdNotificationDeadlineUtc.Should().Be(expectedDeadlineUtc);
        assessment.DataSubjectNotificationDeadlineUtc.Should().Be(expectedDeadlineUtc);

        // 20 dias úteis para o relatório final da ANPD
        assessment.AnpdFinalReportDeadlineUtc.Should().BeAfter(assessment.AnpdNotificationDeadlineUtc!.Value);
    }

    [Fact]
    public void EvaluateIncident_WhenB2BOperatorDataBreached_ShouldEnforce24HourControllerNotificationUnderDpa()
    {
        // Arrange: Incidente envolvendo dados de operadores rurais e vaqueiros (CriaCerto como Operadora)
        var discoveredAt = new DateTime(2026, 10, 27, 9, 30, 0, DateTimeKind.Utc);
        var tenantId = Guid.NewGuid();
        var request = new PrivacyIncidentEvaluationRequest(
            IncidentId: "INC-TEST-003",
            Title: "Acesso indevido a credenciais de vaqueiros e operadores de curral",
            DiscoveredAtUtc: discoveredAt,
            Severity: PrivacyIncidentSeverity.High,
            IsPiiExposedInPlainText: true,
            ImpactedDataSubjectsCount: 12,
            HasFinancialOrCredentialData: true,
            IsB2BOperatorData: true,
            AffectedTenantId: tenantId,
            AffectedTenantName: "Fazenda Esperança"
        );

        // Act
        var result = _evaluator.Evaluate(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var assessment = result.Value;
        assessment.RequiresB2BControllerNotification.Should().BeTrue();
        assessment.B2BControllerNotificationDeadlineUtc.Should().Be(discoveredAt.AddHours(24));
        assessment.AffectedTenantName.Should().Be("Fazenda Esperança");
    }

    [Fact]
    public void GenerateAnpdPreliminaryReport_ShouldEmitCanonicalPayloadMeetingResolution15()
    {
        // Arrange
        var request = new AnpdIncidentReportRequest(
            IncidentProtocol: "INC-20261026-0042",
            DiscoveredAtUtc: new DateTime(2026, 10, 26, 8, 0, 0, DateTimeKind.Utc),
            ContainedAtUtc: new DateTime(2026, 10, 26, 8, 15, 0, DateTimeKind.Utc),
            VulnerabilityVector: "Falha de autorização pontual em endpoint de exportação mitigada por Kill-Switch",
            CompromisedCategories: new[] { "Nome Completo", "CPF", "E-mail" },
            ImpactedSubjectsCount: 45,
            PotentialImpacts: "Risco de tentativas de phishing direcionado e engenharia social",
            ContainmentMeasures: "Ativação de Kill-Switch emergencial (RUNBOOK-01), revogação compulsória de tokens e conferência de integridade SHA-256",
            DataSubjectCommunicationStrategy: "Disparo de e-mail transacional via Resend com orientações de prevenção e publicação de comunicado em /dpo"
        );

        // Act
        var result = _evaluator.GenerateAnpdPreliminaryReport(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var report = result.Value;
        report.Protocol.Should().Be("INC-20261026-0042");
        report.ControllerName.Should().Contain("CriaCerto");
        report.DpoEmail.Should().Be("dpo@criacerto.com.br");
        report.DpoPortalUrl.Should().Contain("/dpo");
        report.ElapsedHoursToContainment.Should().BeApproximately(0.25, 0.01);
        report.CompromisedCategories.Should().Contain("CPF");
        report.RenderMarkdownSummary().Should().Contain("RELATÓRIO PRELIMINAR DE INCIDENTE DE SEGURANÇA (ART. 48 LGPD)");
    }
}
