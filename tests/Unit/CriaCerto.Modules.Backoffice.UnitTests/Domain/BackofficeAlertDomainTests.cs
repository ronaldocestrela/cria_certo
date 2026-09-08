using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Enums;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;
using FluentAssertions;
using Xunit;

namespace CriaCerto.Modules.Backoffice.UnitTests.Domain;

public class BackofficeAlertDomainTests
{
    [Fact]
    public void Create_WithValidData_ShouldReturnSuccessAndActiveAlert()
    {
        // Act
        var result = BackofficeAlert.Create(
            ruleCode: BackofficeAlertRules.PolicyBruteForce,
            title: "Múltiplas falhas de acesso",
            description: "5 tentativas negadas de autorização",
            severity: AlertSeverity.Critical,
            fingerprint: "policy_fail_user_1",
            contextJson: "{\"ip\":\"192.168.1.10\"}",
            targetTenantId: Guid.NewGuid(),
            targetTenantName: "Fazenda Estrela",
            relatedAdminUserId: Guid.NewGuid(),
            relatedAdminEmail: "operator@criacerto.com.br"
        );

        // Assert
        result.IsSuccess.Should().BeTrue();
        var alert = result.Value;
        alert.Id.Should().NotBeEmpty();
        alert.RuleCode.Should().Be(BackofficeAlertRules.PolicyBruteForce);
        alert.Title.Should().Be("Múltiplas falhas de acesso");
        alert.Severity.Should().Be(AlertSeverity.Critical);
        alert.Status.Should().Be(AlertStatus.Active);
        alert.OccurrenceCount.Should().Be(1);
        alert.Fingerprint.Should().Be("policy_fail_user_1");
        alert.ContextJson.Should().Be("{\"ip\":\"192.168.1.10\"}");
        alert.TargetTenantName.Should().Be("Fazenda Estrela");
        alert.RelatedAdminEmail.Should().Be("operator@criacerto.com.br");
        alert.AcknowledgedAtUtc.Should().BeNull();
        alert.ResolvedAtUtc.Should().BeNull();
    }

    [Theory]
    [InlineData("", "Title")]
    [InlineData("   ", "Title")]
    [InlineData(null, "Title")]
    public void Create_WithoutRuleCode_ShouldReturnFailure(string? ruleCode, string title)
    {
        // Act
        var result = BackofficeAlert.Create(
            ruleCode: ruleCode!,
            title: title,
            description: "desc",
            severity: AlertSeverity.Warning,
            fingerprint: "fp1"
        );

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ObservabilityErrors.RuleCodeRequired.Code);
    }

    [Theory]
    [InlineData("RULE_1", "")]
    [InlineData("RULE_1", "   ")]
    [InlineData("RULE_1", null)]
    public void Create_WithoutTitle_ShouldReturnFailure(string ruleCode, string? title)
    {
        // Act
        var result = BackofficeAlert.Create(
            ruleCode: ruleCode,
            title: title!,
            description: "desc",
            severity: AlertSeverity.Warning,
            fingerprint: "fp1"
        );

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ObservabilityErrors.TitleRequired.Code);
    }

    [Fact]
    public void IncrementOccurrence_OnActiveAlert_ShouldIncreaseCountAndKeepActive()
    {
        // Arrange
        var alert = BackofficeAlert.Create(
            BackofficeAlertRules.ImpersonationBurst,
            "Surto de Impersonação",
            "Múltiplas sessões",
            AlertSeverity.Warning,
            "burst_1"
        ).Value;

        // Act
        var result = alert.IncrementOccurrence("{\"updated\":true}");

        // Assert
        result.IsSuccess.Should().BeTrue();
        alert.OccurrenceCount.Should().Be(2);
        alert.ContextJson.Should().Be("{\"updated\":true}");
        alert.Status.Should().Be(AlertStatus.Active);
    }

    [Fact]
    public void Acknowledge_ValidAdmin_ShouldTransitionToAcknowledged()
    {
        // Arrange
        var alert = BackofficeAlert.Create(
            BackofficeAlertRules.OffHoursCriticalAction,
            "Ação Crítica Fora de Horário",
            "Expurgo acionado às 23:00",
            AlertSeverity.Critical,
            "offhours_1"
        ).Value;
        var adminId = Guid.NewGuid();
        var email = "admin@criacerto.com.br";

        // Act
        var result = alert.Acknowledge(adminId, email);

        // Assert
        result.IsSuccess.Should().BeTrue();
        alert.Status.Should().Be(AlertStatus.Acknowledged);
        alert.AcknowledgedByAdminUserId.Should().Be(adminId);
        alert.AcknowledgedByEmail.Should().Be(email);
        alert.AcknowledgedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Resolve_ValidResolutionNotes_ShouldTransitionToResolved()
    {
        // Arrange
        var alert = BackofficeAlert.Create(
            BackofficeAlertRules.PolicyBruteForce,
            "Falhas consecutivas",
            "desc",
            AlertSeverity.Warning,
            "fp"
        ).Value;
        var adminId = Guid.NewGuid();
        var email = "support@criacerto.com.br";

        // Act
        var result = alert.Resolve(adminId, email, "IP bloqueado temporariamente no firewall.");

        // Assert
        result.IsSuccess.Should().BeTrue();
        alert.Status.Should().Be(AlertStatus.Resolved);
        alert.ResolvedByAdminUserId.Should().Be(adminId);
        alert.ResolvedByEmail.Should().Be(email);
        alert.ResolvedAtUtc.Should().NotBeNull();
        alert.ResolutionNotes.Should().Be("IP bloqueado temporariamente no firewall.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("curt")] // less than 5 characters
    [InlineData(null)]
    public void Resolve_WithInvalidNotes_ShouldReturnFailure(string? notes)
    {
        // Arrange
        var alert = BackofficeAlert.Create(
            BackofficeAlertRules.PolicyBruteForce,
            "Falhas consecutivas",
            "desc",
            AlertSeverity.Warning,
            "fp"
        ).Value;

        // Act
        var result = alert.Resolve(Guid.NewGuid(), "admin@criacerto.com.br", notes!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ObservabilityErrors.ResolutionNotesRequired.Code);
        alert.Status.Should().Be(AlertStatus.Active);
    }

    [Fact]
    public void IncrementOccurrence_Or_Acknowledge_OnResolvedAlert_ShouldFail()
    {
        // Arrange
        var alert = BackofficeAlert.Create(
            BackofficeAlertRules.PolicyBruteForce,
            "Falhas",
            "desc",
            AlertSeverity.Warning,
            "fp"
        ).Value;
        alert.Resolve(Guid.NewGuid(), "admin@criacerto.com.br", "Resolvido com sucesso.").IsSuccess.Should().BeTrue();

        // Act
        var ackResult = alert.Acknowledge(Guid.NewGuid(), "admin2@criacerto.com.br");
        var incResult = alert.IncrementOccurrence();

        // Assert
        ackResult.IsFailure.Should().BeTrue();
        ackResult.Error.Code.Should().Be(ObservabilityErrors.CannotAcknowledgeResolved.Code);

        incResult.IsFailure.Should().BeTrue();
        incResult.Error.Code.Should().Be(ObservabilityErrors.AlreadyResolved.Code);
    }
}
