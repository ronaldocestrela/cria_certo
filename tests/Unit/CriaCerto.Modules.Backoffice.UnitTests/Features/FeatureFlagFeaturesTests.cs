using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Enums;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;
using CriaCerto.Modules.Backoffice.Application.Domain.Services;
using CriaCerto.Modules.Backoffice.Application.Features.Rollout.Commands;
using CriaCerto.Modules.Backoffice.Application.Features.Rollout.Queries;
using CriaCerto.Modules.Backoffice.Application.Security;
using CriaCerto.Modules.Backoffice.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CriaCerto.Modules.Backoffice.UnitTests.Features;

public class FeatureFlagFeaturesTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BackofficeDbContext _dbContext;
    private readonly IFeatureFlagEvaluator _evaluator;

    public FeatureFlagFeaturesTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<BackofficeDbContext>().UseSqlite(_connection).Options;
        _dbContext = new BackofficeDbContext(options);
        _dbContext.Database.EnsureCreated();
        _evaluator = new FeatureFlagEvaluator();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Close();
        _connection.Dispose();
    }

    private async Task<FeatureFlag> SeedFlagAsync(
        string key = "backoffice.feature.impersonation",
        string name = "Suporte Assistido",
        RolloutRing ring = RolloutRing.Ring0_Canary,
        int percentage = 0)
    {
        var flag = FeatureFlag.Create(
            key: key,
            name: name,
            description: "Descrição de teste",
            category: FeatureFlagCategory.SupportTools,
            initialRing: ring,
            initialPercentage: percentage,
            createdBy: "admin@criacerto.com.br"
        ).Value;

        _dbContext.FeatureFlags.Add(flag);
        await _dbContext.SaveChangesAsync();
        return flag;
    }

    [Fact]
    public async Task ToggleFeatureFlag_WhenJustificationTooShort_ShouldReturnFailure()
    {
        // Arrange
        await SeedFlagAsync();
        var handler = new ToggleFeatureFlagCommandHandler(_dbContext);
        var command = new ToggleFeatureFlagCommand(
            FlagKey: "backoffice.feature.impersonation",
            IsEnabled: false,
            Reason: "curto",
            ActorId: Guid.NewGuid(),
            ActorEmail: "admin@criacerto.com.br",
            ActorRole: "PlatformOwner",
            IpAddress: "127.0.0.1",
            UserAgent: "xUnit"
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("FeatureFlag.JustificationTooShort");
    }

    [Fact]
    public async Task ToggleFeatureFlag_WithValidData_ShouldUpdateAndRecordAuditLog()
    {
        // Arrange
        await SeedFlagAsync();
        var handler = new ToggleFeatureFlagCommandHandler(_dbContext);
        var command = new ToggleFeatureFlagCommand(
            FlagKey: "backoffice.feature.impersonation",
            IsEnabled: false,
            Reason: "Manutenção emergencial da ferramenta de suporte",
            ActorId: Guid.NewGuid(),
            ActorEmail: "admin@criacerto.com.br",
            ActorRole: "PlatformOwner",
            IpAddress: "127.0.0.1",
            UserAgent: "xUnit"
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsEnabled.Should().BeFalse();
        result.Value.UpdatedBy.Should().Be("admin@criacerto.com.br");

        // Verifica gravação no AuditLog
        var audit = await _dbContext.AuditLogs.OrderByDescending(a => a.TimestampUtc).FirstOrDefaultAsync();
        audit.Should().NotBeNull();
        audit!.Action.Should().Be("FEATURE_FLAG_TOGGLED");
        audit.Category.Should().Be(AuditCategory.Rollout);
        audit.Severity.Should().Be(AuditSeverity.High);
        audit.RecordHash.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task UpdateRolloutWave_ShouldUpdatePercentageAndRings()
    {
        // Arrange
        await SeedFlagAsync();
        var handler = new UpdateRolloutWaveCommandHandler(_dbContext);
        var command = new UpdateRolloutWaveCommand(
            FlagKey: "backoffice.feature.impersonation",
            Percentage: 75,
            MaxAllowedRing: RolloutRing.Ring1_EarlyAdopters,
            WhitelistedEmails: new List<string> { "tester@criacerto.com.br" },
            Reason: "Avanço planejado para o anel de Early Adopters (Ring 1)",
            ActorId: Guid.NewGuid(),
            ActorEmail: "admin@criacerto.com.br",
            ActorRole: "PlatformOwner",
            IpAddress: "127.0.0.1",
            UserAgent: "xUnit"
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.RolloutPercentage.Should().Be(75);
        result.Value.MaxAllowedRing.Should().Be(RolloutRing.Ring1_EarlyAdopters);
        result.Value.WhitelistedEmails.Should().Contain("tester@criacerto.com.br");

        var audit = await _dbContext.AuditLogs.OrderByDescending(a => a.TimestampUtc).FirstOrDefaultAsync();
        audit.Should().NotBeNull();
        audit!.Action.Should().Be("ROLLOUT_WAVE_UPDATED");
    }

    [Fact]
    public async Task TriggerKillSwitch_ShouldImmediatelyHaltAndAuditWithCriticalSeverity()
    {
        // Arrange
        await SeedFlagAsync(ring: RolloutRing.Ring2_GeneralAvailability, percentage: 100);
        var handler = new TriggerKillSwitchCommandHandler(_dbContext);
        var command = new TriggerKillSwitchCommand(
            FlagKey: "backoffice.feature.impersonation",
            Reason: "Vulnerabilidade detectada em sessão ativa de impersonação",
            ActorId: Guid.NewGuid(),
            ActorEmail: "secops@criacerto.com.br",
            ActorRole: "PlatformOwner",
            IpAddress: "10.0.0.1",
            UserAgent: "SecOps Alert"
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.KillSwitchActive.Should().BeTrue();
        result.Value.KillSwitchReason.Should().Be("Vulnerabilidade detectada em sessão ativa de impersonação");

        var audit = await _dbContext.AuditLogs.OrderByDescending(a => a.TimestampUtc).FirstOrDefaultAsync();
        audit.Should().NotBeNull();
        audit!.Action.Should().Be("KILL_SWITCH_ACTIVATED");
        audit.Severity.Should().Be(AuditSeverity.Critical);
    }

    [Fact]
    public async Task DeactivateKillSwitch_ShouldRestoreOperationalFlag()
    {
        // Arrange
        var flag = await SeedFlagAsync(ring: RolloutRing.Ring2_GeneralAvailability, percentage: 100);
        flag.TriggerKillSwitch("Incidente crítico ativo", "secops@criacerto.com.br");
        await _dbContext.SaveChangesAsync();

        var handler = new DeactivateKillSwitchCommandHandler(_dbContext);
        var command = new DeactivateKillSwitchCommand(
            FlagKey: "backoffice.feature.impersonation",
            Reason: "Mitigação concluída e validada em ambiente isolado",
            ActorId: Guid.NewGuid(),
            ActorEmail: "owner@criacerto.com.br",
            ActorRole: "PlatformOwner",
            IpAddress: "10.0.0.1",
            UserAgent: "SecOps Alert"
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.KillSwitchActive.Should().BeFalse();
        result.Value.KillSwitchReason.Should().BeNull();
    }

    [Fact]
    public async Task GetFeatureFlagsQuery_ShouldReturnAllRegisteredFlags()
    {
        // Arrange
        await SeedFlagAsync("flag1", "Flag 1");
        await SeedFlagAsync("flag2", "Flag 2");
        var handler = new GetFeatureFlagsQueryHandler(_dbContext);

        // Act
        var result = await handler.Handle(new GetFeatureFlagsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task EvaluateFeatureFlagQuery_ShouldReturnAccurateStatus()
    {
        // Arrange
        await SeedFlagAsync("flag_eval", "Flag Eval", RolloutRing.Ring0_Canary, 100);
        var handler = new EvaluateFeatureFlagQueryHandler(_dbContext, _evaluator);

        // Act
        var resultOwner = await handler.Handle(
            new EvaluateFeatureFlagQuery("flag_eval", "owner@criacerto.com.br", "PlatformOwner"),
            CancellationToken.None);

        var resultN1 = await handler.Handle(
            new EvaluateFeatureFlagQuery("flag_eval", "n1@criacerto.com.br", "SupportN1"),
            CancellationToken.None);

        // Assert
        resultOwner.IsSuccess.Should().BeTrue();
        resultOwner.Value.Should().BeTrue();

        resultN1.IsSuccess.Should().BeTrue();
        resultN1.Value.Should().BeFalse();
    }

    [Fact]
    public async Task GetRolloutSloHealthQuery_ShouldReturnHealthMetrics()
    {
        // Arrange
        await SeedFlagAsync("backoffice.feature.impersonation", "Impersonação");
        var handler = new GetRolloutSloHealthQueryHandler(_dbContext);

        // Act
        var result = await handler.Handle(new GetRolloutSloHealthQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        var health = result.Value.First();
        health.FlagKey.Should().Be("backoffice.feature.impersonation");
        health.IsHealthy.Should().BeTrue();
        health.CurrentErrorRatePercent.Should().BeLessThanOrEqualTo(1.0);
    }

    [Fact]
    public async Task FeatureFlagEvaluationBehavior_WhenKillSwitchActive_ShouldAbortWithFailure()
    {
        // Arrange
        var flag = await SeedFlagAsync("feature.critical.test", "Critical Test", RolloutRing.Ring2_GeneralAvailability, 100);
        flag.TriggerKillSwitch("Corte de emergência", "secops@criacerto.com.br");
        await _dbContext.SaveChangesAsync();

        var behavior = new FeatureFlagEvaluationBehavior<DummyCriticalProtectedCommand, Result>(
            _dbContext,
            _evaluator
        );

        var command = new DummyCriticalProtectedCommand(
            ActorId: Guid.NewGuid(),
            ActorEmail: "owner@criacerto.com.br",
            ActorRole: "PlatformOwner"
        );

        var nextCalled = false;
        Task<Result> Next()
        {
            nextCalled = true;
            return Task.FromResult(Result.Success());
        }

        // Act
        var response = await behavior.Handle(command, Next, CancellationToken.None);

        // Assert
        nextCalled.Should().BeFalse();
        response.IsFailure.Should().BeTrue();
        response.Error.Code.Should().Be("FeatureFlag.KillSwitchActive");
    }

    [Fact]
    public async Task FeatureFlagEvaluationBehavior_WhenWaveNotReached_ShouldAbortWithFailure()
    {
        // Arrange: Flag em Ring 0 (Canary) e usuário com perfil SupportN1
        await SeedFlagAsync("feature.critical.test", "Critical Test", RolloutRing.Ring0_Canary, 100);

        var behavior = new FeatureFlagEvaluationBehavior<DummyCriticalProtectedCommand, Result>(
            _dbContext,
            _evaluator
        );

        var command = new DummyCriticalProtectedCommand(
            ActorId: Guid.NewGuid(),
            ActorEmail: "n1@criacerto.com.br",
            ActorRole: "SupportN1"
        );

        var nextCalled = false;
        Task<Result> Next()
        {
            nextCalled = true;
            return Task.FromResult(Result.Success());
        }

        // Act
        var response = await behavior.Handle(command, Next, CancellationToken.None);

        // Assert
        nextCalled.Should().BeFalse();
        response.IsFailure.Should().BeTrue();
        response.Error.Code.Should().Be("FeatureFlag.WaveNotReached");
    }

    [Fact]
    public async Task FeatureFlagEvaluationBehavior_WhenEligible_ShouldExecuteNext()
    {
        // Arrange: Flag em Ring 0 (Canary) e usuário PlatformOwner
        await SeedFlagAsync("feature.critical.test", "Critical Test", RolloutRing.Ring0_Canary, 100);

        var behavior = new FeatureFlagEvaluationBehavior<DummyCriticalProtectedCommand, Result>(
            _dbContext,
            _evaluator
        );

        var command = new DummyCriticalProtectedCommand(
            ActorId: Guid.NewGuid(),
            ActorEmail: "owner@criacerto.com.br",
            ActorRole: "PlatformOwner"
        );

        var nextCalled = false;
        Task<Result> Next()
        {
            nextCalled = true;
            return Task.FromResult(Result.Success());
        }

        // Act
        var response = await behavior.Handle(command, Next, CancellationToken.None);

        // Assert
        nextCalled.Should().BeTrue();
        response.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task FeatureFlagEvaluationBehavior_WhenPublishPlanVersionWithPlatformOwnerRole_ShouldAllow()
    {
        // Arrange: Flag backoffice.feature.plan_publishing em Ring 1 (Early Adopters)
        await SeedFlagAsync(
            key: "backoffice.feature.plan_publishing",
            name: "Publicação de Planos",
            ring: RolloutRing.Ring1_EarlyAdopters,
            percentage: 100);

        var behavior = new FeatureFlagEvaluationBehavior<CriaCerto.Modules.Backoffice.Application.Features.Plans.Commands.PublishPlanVersionCommand, Result<CriaCerto.Modules.Backoffice.Application.Features.Plans.Dtos.PlanVersionDto>>(
            _dbContext,
            _evaluator
        );

        var command = new CriaCerto.Modules.Backoffice.Application.Features.Plans.Commands.PublishPlanVersionCommand(
            VersionId: Guid.NewGuid(),
            ApprovalNotes: "Lançamento oficial",
            PerformedByAdminUserId: Guid.NewGuid(),
            PerformedByAdminEmail: "owner@criacerto.com.br",
            IpAddress: "127.0.0.1",
            ActorRole: "PlatformOwner"
        );

        var nextCalled = false;
        Task<Result<CriaCerto.Modules.Backoffice.Application.Features.Plans.Dtos.PlanVersionDto>> Next()
        {
            nextCalled = true;
            return Task.FromResult(Result.Success(new CriaCerto.Modules.Backoffice.Application.Features.Plans.Dtos.PlanVersionDto(
                command.VersionId,
                Guid.NewGuid(),
                1,
                "v1.0",
                "Published",
                100m,
                80m,
                100,
                null,
                null,
                null,
                null,
                DateTimeOffset.UtcNow,
                Guid.NewGuid(),
                "Aprovado",
                DateTimeOffset.UtcNow,
                Array.Empty<CriaCerto.Modules.Backoffice.Application.Features.Plans.Dtos.PlanFeatureDto>(),
                Array.Empty<CriaCerto.Modules.Backoffice.Application.Features.Plans.Dtos.PlanLimitDto>()
            )));
        }

        // Act
        var response = await behavior.Handle(command, Next, CancellationToken.None);

        // Assert
        nextCalled.Should().BeTrue();
        response.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task FeatureFlagEvaluationBehavior_WhenPublishPlanVersionWithSupportN1Role_ShouldFailWaveNotReached()
    {
        // Arrange: Flag backoffice.feature.plan_publishing em Ring 1 (Early Adopters)
        await SeedFlagAsync(
            key: "backoffice.feature.plan_publishing",
            name: "Publicação de Planos",
            ring: RolloutRing.Ring1_EarlyAdopters,
            percentage: 100);

        var behavior = new FeatureFlagEvaluationBehavior<CriaCerto.Modules.Backoffice.Application.Features.Plans.Commands.PublishPlanVersionCommand, Result<CriaCerto.Modules.Backoffice.Application.Features.Plans.Dtos.PlanVersionDto>>(
            _dbContext,
            _evaluator
        );

        var command = new CriaCerto.Modules.Backoffice.Application.Features.Plans.Commands.PublishPlanVersionCommand(
            VersionId: Guid.NewGuid(),
            ApprovalNotes: "Tentativa não autorizada",
            PerformedByAdminUserId: Guid.NewGuid(),
            PerformedByAdminEmail: "n1@criacerto.com.br",
            IpAddress: "127.0.0.1",
            ActorRole: "SupportN1"
        );

        var nextCalled = false;
        Task<Result<CriaCerto.Modules.Backoffice.Application.Features.Plans.Dtos.PlanVersionDto>> Next()
        {
            nextCalled = true;
            return Task.FromResult(Result.Success<CriaCerto.Modules.Backoffice.Application.Features.Plans.Dtos.PlanVersionDto>(null!));
        }

        // Act
        var response = await behavior.Handle(command, Next, CancellationToken.None);

        // Assert
        nextCalled.Should().BeFalse();
        response.IsFailure.Should().BeTrue();
        response.Error.Code.Should().Be("FeatureFlag.WaveNotReached");
    }
}

[RequireFeatureFlag("feature.critical.test")]
public record DummyCriticalProtectedCommand(
    Guid ActorId,
    string ActorEmail,
    string? ActorRole
) : IRequest<Result>, IBackofficeActorRequest;
