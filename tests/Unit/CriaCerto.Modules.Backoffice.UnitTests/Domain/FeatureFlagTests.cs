using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Enums;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;
using CriaCerto.Modules.Backoffice.Application.Domain.Services;
using FluentAssertions;
using Xunit;

namespace CriaCerto.Modules.Backoffice.UnitTests.Domain;

public class FeatureFlagTests
{
    [Fact]
    public void Create_WithValidData_ShouldReturnSuccess()
    {
        // Act
        var result = FeatureFlag.Create(
            key: "backoffice.feature.impersonation",
            name: "Suporte Assistido com Impersonação",
            description: "Permite que operadores de suporte acessem o ambiente do produtor para diagnóstico",
            category: FeatureFlagCategory.SupportTools,
            initialRing: RolloutRing.Ring0_Canary,
            initialPercentage: 0,
            createdBy: "admin@criacerto.com.br"
        );

        // Assert
        result.IsSuccess.Should().BeTrue();
        var flag = result.Value;
        flag.Id.Should().NotBeEmpty();
        flag.Key.Should().Be("backoffice.feature.impersonation");
        flag.Name.Should().Be("Suporte Assistido com Impersonação");
        flag.Category.Should().Be(FeatureFlagCategory.SupportTools);
        flag.IsEnabled.Should().BeTrue();
        flag.RolloutPercentage.Should().Be(0);
        flag.MaxAllowedRing.Should().Be(RolloutRing.Ring0_Canary);
        flag.KillSwitchActive.Should().BeFalse();
        flag.KillSwitchReason.Should().BeNull();
        flag.CreatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Theory]
    [InlineData("", "Nome")]
    [InlineData("   ", "Nome")]
    [InlineData(null, "Nome")]
    public void Create_WithoutKey_ShouldReturnFailure(string? key, string name)
    {
        // Act
        var result = FeatureFlag.Create(
            key: key!,
            name: name,
            description: "Desc",
            category: FeatureFlagCategory.CriticalOperation,
            initialRing: RolloutRing.Ring0_Canary,
            initialPercentage: 10,
            createdBy: "admin@criacerto.com.br"
        );

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("FeatureFlag.KeyRequired");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Create_WithInvalidPercentage_ShouldReturnFailure(int percentage)
    {
        // Act
        var result = FeatureFlag.Create(
            key: "backoffice.feature.test",
            name: "Test",
            description: "Desc",
            category: FeatureFlagCategory.SupportTools,
            initialRing: RolloutRing.Ring0_Canary,
            initialPercentage: percentage,
            createdBy: "admin@criacerto.com.br"
        );

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("FeatureFlag.InvalidRolloutPercentage");
    }

    [Fact]
    public void Toggle_ShouldUpdateStatusAndRecordAuditMetadata()
    {
        // Arrange
        var flag = FeatureFlag.Create(
            key: "backoffice.feature.test",
            name: "Test",
            description: "Desc",
            category: FeatureFlagCategory.SupportTools,
            initialRing: RolloutRing.Ring1_EarlyAdopters,
            initialPercentage: 50,
            createdBy: "admin@criacerto.com.br"
        ).Value;

        // Act
        var toggleResult = flag.Toggle(false, "ops@criacerto.com.br", "Desativação planejada para manutenção");

        // Assert
        toggleResult.IsSuccess.Should().BeTrue();
        flag.IsEnabled.Should().BeFalse();
        flag.UpdatedBy.Should().Be("ops@criacerto.com.br");
        flag.UpdatedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void TriggerKillSwitch_ShouldImmediatelyDisableAndRecordReason()
    {
        // Arrange
        var flag = FeatureFlag.Create(
            key: "backoffice.feature.plan_publishing",
            name: "Publicação de Planos",
            description: "Desc",
            category: FeatureFlagCategory.CommercialAndPlans,
            initialRing: RolloutRing.Ring2_GeneralAvailability,
            initialPercentage: 100,
            createdBy: "admin@criacerto.com.br"
        ).Value;

        // Act
        var killResult = flag.TriggerKillSwitch(
            reason: "Anomalia detectada no cálculo de faturamento durante publicação",
            activatedBy: "secops@criacerto.com.br"
        );

        // Assert
        killResult.IsSuccess.Should().BeTrue();
        flag.KillSwitchActive.Should().BeTrue();
        flag.KillSwitchReason.Should().Be("Anomalia detectada no cálculo de faturamento durante publicação");
        flag.KillSwitchActivatedBy.Should().Be("secops@criacerto.com.br");
        flag.KillSwitchActivatedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void TriggerKillSwitch_WithoutReason_ShouldReturnFailure()
    {
        // Arrange
        var flag = FeatureFlag.Create(
            key: "backoffice.feature.test",
            name: "Test",
            description: "Desc",
            category: FeatureFlagCategory.CommercialAndPlans,
            initialRing: RolloutRing.Ring2_GeneralAvailability,
            initialPercentage: 100,
            createdBy: "admin@criacerto.com.br"
        ).Value;

        // Act
        var killResult = flag.TriggerKillSwitch(
            reason: "curto", // menos de 10 caracteres
            activatedBy: "secops@criacerto.com.br"
        );

        // Assert
        killResult.IsFailure.Should().BeTrue();
        killResult.Error.Code.Should().Be("FeatureFlag.JustificationTooShort");
    }

    [Fact]
    public void DeactivateKillSwitch_ShouldRestoreOperationalStatus()
    {
        // Arrange
        var flag = FeatureFlag.Create(
            key: "backoffice.feature.test",
            name: "Test",
            description: "Desc",
            category: FeatureFlagCategory.CommercialAndPlans,
            initialRing: RolloutRing.Ring2_GeneralAvailability,
            initialPercentage: 100,
            createdBy: "admin@criacerto.com.br"
        ).Value;

        flag.TriggerKillSwitch("Incidente crítico sob mitigação", "secops@criacerto.com.br");
        flag.KillSwitchActive.Should().BeTrue();

        // Act
        var restoreResult = flag.DeactivateKillSwitch(
            restoredBy: "platform_owner@criacerto.com.br",
            reason: "Patch de segurança aplicado e validado com sucesso"
        );

        // Assert
        restoreResult.IsSuccess.Should().BeTrue();
        flag.KillSwitchActive.Should().BeFalse();
        flag.KillSwitchReason.Should().BeNull();
        flag.KillSwitchActivatedBy.Should().BeNull();
        flag.KillSwitchActivatedAtUtc.Should().BeNull();
        flag.UpdatedBy.Should().Be("platform_owner@criacerto.com.br");
    }

    [Fact]
    public void Evaluator_WhenKillSwitchActive_ShouldAlwaysReturnFalse()
    {
        // Arrange
        var flag = FeatureFlag.Create(
            key: "backoffice.feature.test",
            name: "Test",
            description: "Desc",
            category: FeatureFlagCategory.CriticalOperation,
            initialRing: RolloutRing.Ring2_GeneralAvailability,
            initialPercentage: 100,
            createdBy: "admin@criacerto.com.br"
        ).Value;

        flag.TriggerKillSwitch("Emergência de teste", "admin@criacerto.com.br");

        var evaluator = new FeatureFlagEvaluator();

        // Act & Assert
        evaluator.Evaluate(flag, "owner@criacerto.com.br", "PlatformOwner").Should().BeFalse();
        evaluator.Evaluate(flag, "n1@criacerto.com.br", "SupportN1").Should().BeFalse();
    }

    [Fact]
    public void Evaluator_WhenFlagGloballyDisabled_ShouldReturnFalse()
    {
        // Arrange
        var flag = FeatureFlag.Create(
            key: "backoffice.feature.test",
            name: "Test",
            description: "Desc",
            category: FeatureFlagCategory.CriticalOperation,
            initialRing: RolloutRing.Ring2_GeneralAvailability,
            initialPercentage: 100,
            createdBy: "admin@criacerto.com.br"
        ).Value;

        flag.Toggle(false, "admin@criacerto.com.br", "Desativada globalmente");

        var evaluator = new FeatureFlagEvaluator();

        // Act & Assert
        evaluator.Evaluate(flag, "owner@criacerto.com.br", "PlatformOwner").Should().BeFalse();
    }

    [Fact]
    public void Evaluator_Ring0_ShouldAllowOnlyPlatformOwner()
    {
        // Arrange
        var flag = FeatureFlag.Create(
            key: "backoffice.feature.test",
            name: "Test",
            description: "Desc",
            category: FeatureFlagCategory.CriticalOperation,
            initialRing: RolloutRing.Ring0_Canary,
            initialPercentage: 100,
            createdBy: "admin@criacerto.com.br"
        ).Value;

        var evaluator = new FeatureFlagEvaluator();

        // Act & Assert
        evaluator.Evaluate(flag, "owner@criacerto.com.br", "PlatformOwner").Should().BeTrue();
        evaluator.Evaluate(flag, "n2@criacerto.com.br", "SupportN2").Should().BeFalse();
        evaluator.Evaluate(flag, "n1@criacerto.com.br", "SupportN1").Should().BeFalse();
    }

    [Fact]
    public void Evaluator_Ring1_ShouldAllowPlatformOwnerAndEarlyAdopterRoles()
    {
        // Arrange
        var flag = FeatureFlag.Create(
            key: "backoffice.feature.test",
            name: "Test",
            description: "Desc",
            category: FeatureFlagCategory.CriticalOperation,
            initialRing: RolloutRing.Ring1_EarlyAdopters,
            initialPercentage: 100,
            createdBy: "admin@criacerto.com.br"
        ).Value;

        var evaluator = new FeatureFlagEvaluator();

        // Act & Assert
        evaluator.Evaluate(flag, "owner@criacerto.com.br", "PlatformOwner").Should().BeTrue();
        evaluator.Evaluate(flag, "n2@criacerto.com.br", "SupportN2").Should().BeTrue();
        evaluator.Evaluate(flag, "fin@criacerto.com.br", "FinanceOps").Should().BeTrue();
        evaluator.Evaluate(flag, "n1@criacerto.com.br", "SupportN1").Should().BeFalse();
        evaluator.Evaluate(flag, "auditor@criacerto.com.br", "ReadOnlyAuditor").Should().BeFalse();
    }

    [Fact]
    public void Evaluator_Ring2_ShouldAllowAllRoles()
    {
        // Arrange
        var flag = FeatureFlag.Create(
            key: "backoffice.feature.test",
            name: "Test",
            description: "Desc",
            category: FeatureFlagCategory.CriticalOperation,
            initialRing: RolloutRing.Ring2_GeneralAvailability,
            initialPercentage: 100,
            createdBy: "admin@criacerto.com.br"
        ).Value;

        var evaluator = new FeatureFlagEvaluator();

        // Act & Assert
        evaluator.Evaluate(flag, "owner@criacerto.com.br", "PlatformOwner").Should().BeTrue();
        evaluator.Evaluate(flag, "n2@criacerto.com.br", "SupportN2").Should().BeTrue();
        evaluator.Evaluate(flag, "n1@criacerto.com.br", "SupportN1").Should().BeTrue();
        evaluator.Evaluate(flag, "auditor@criacerto.com.br", "ReadOnlyAuditor").Should().BeTrue();
    }

    [Fact]
    public void Evaluator_Whitelist_ShouldBypassRingAndPercentage()
    {
        // Arrange
        var flag = FeatureFlag.Create(
            key: "backoffice.feature.test",
            name: "Test",
            description: "Desc",
            category: FeatureFlagCategory.CriticalOperation,
            initialRing: RolloutRing.Ring0_Canary,
            initialPercentage: 0,
            createdBy: "admin@criacerto.com.br"
        ).Value;

        flag.SetWhitelistedEmails(new[] { "beta_tester@criacerto.com.br" });

        var evaluator = new FeatureFlagEvaluator();

        // Act & Assert
        evaluator.Evaluate(flag, "beta_tester@criacerto.com.br", "SupportN1").Should().BeTrue();
        evaluator.Evaluate(flag, "other_n1@criacerto.com.br", "SupportN1").Should().BeFalse();
    }

    [Fact]
    public void Evaluator_DeterministicPercentage_ShouldProduceConsistentBucket()
    {
        // Arrange
        var flag = FeatureFlag.Create(
            key: "backoffice.feature.test",
            name: "Test",
            description: "Desc",
            category: FeatureFlagCategory.CriticalOperation,
            initialRing: RolloutRing.Ring2_GeneralAvailability,
            initialPercentage: 50,
            createdBy: "admin@criacerto.com.br"
        ).Value;

        var evaluator = new FeatureFlagEvaluator();

        // Act: Avaliar múltiplas vezes para o mesmo usuário
        var eval1 = evaluator.Evaluate(flag, "user_stable@criacerto.com.br", "SupportN1");
        var eval2 = evaluator.Evaluate(flag, "user_stable@criacerto.com.br", "SupportN1");
        var eval3 = evaluator.Evaluate(flag, "user_stable@criacerto.com.br", "SupportN1");

        // Assert: Deve ser determinístico
        eval1.Should().Be(eval2);
        eval2.Should().Be(eval3);
    }
}
