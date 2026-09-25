using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Enums;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;
using CriaCerto.Modules.Backoffice.Application.Domain.Services;
using CriaCerto.Modules.Backoffice.Application.Features.Approvals.Commands;
using CriaCerto.Modules.Backoffice.Application.Features.Audit.Queries;
using CriaCerto.Modules.Backoffice.Application.Features.Compliance.Commands;
using CriaCerto.Modules.Backoffice.Application.Features.Compliance.Dtos;
using CriaCerto.Modules.Backoffice.Application.Features.Rollout.Commands;
using CriaCerto.Modules.Backoffice.Infrastructure.Persistence;
using CriaCerto.Modules.Tenancy.Application.Contracts;
using CriaCerto.Modules.Tenancy.Application.Features.BackofficeTenants;
using FluentAssertions;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace CriaCerto.Modules.Backoffice.UnitTests.Operations;

/// <summary>
/// Executable living documentation and automated simulation tests for Backoffice Sub-phase 6.3:
/// Incident Response Runbooks, 4-Eyes Governance, Forensics and Compliance safeguards.
/// </summary>
public class IncidentResponseSimulationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BackofficeDbContext _dbContext;
    private readonly IFeatureFlagEvaluator _evaluator;
    private readonly ISender _sender;
    private readonly IPiiDataMasker _masker;

    public IncidentResponseSimulationTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<BackofficeDbContext>().UseSqlite(_connection).Options;
        _dbContext = new BackofficeDbContext(options);
        _dbContext.Database.EnsureCreated();

        _evaluator = new FeatureFlagEvaluator();
        _sender = Substitute.For<ISender>();
        _masker = new PiiDataMasker();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Close();
        _connection.Dispose();
    }

    [Fact]
    public async Task Runbook01_KillSwitchEmergencyContainment_ShouldBlockFeatureInstantlyAndLogCriticalAudit()
    {
        // 1. Arrange: provision feature flag for remediation execution in Ring2 (General Availability)
        const string flagKey = "backoffice.feature.remediation_execution";
        var flag = FeatureFlag.Create(
            key: flagKey,
            name: "Remediação Operacional de Suporte",
            description: "Permite que N2 execute correções no tenant",
            category: FeatureFlagCategory.SupportTools,
            initialRing: RolloutRing.Ring2_GeneralAvailability,
            initialPercentage: 100,
            createdBy: "admin@criacerto.com.br"
        ).Value;

        _dbContext.FeatureFlags.Add(flag);
        await _dbContext.SaveChangesAsync();

        // Verify feature is initially active for operator
        var evalBefore = _evaluator.Evaluate(flag, "operator@criacerto.com.br", "SupportN2");
        evalBefore.Should().BeTrue();

        // 2. Act: Trigger RUNBOOK-01 emergency containment (Kill-Switch)
        var killSwitchHandler = new TriggerKillSwitchCommandHandler(_dbContext);
        var command = new TriggerKillSwitchCommand(
            FlagKey: flagKey,
            Reason: "ALERTA P1: Degradação de SLO com taxa de erro > 10% detectada na remediação",
            ActorId: Guid.NewGuid(),
            ActorEmail: "secops@criacerto.com.br",
            ActorRole: "PlatformOwner",
            IpAddress: "10.0.0.1",
            UserAgent: "IncidentResponse-Drill"
        );

        var result = await killSwitchHandler.Handle(command, CancellationToken.None);

        // 3. Assert: Verify immediate containment
        result.IsSuccess.Should().BeTrue();
        result.Value.KillSwitchActive.Should().BeTrue();
        result.Value.KillSwitchReason.Should().Contain("ALERTA P1");

        // Subsequent evaluation must evaluate to FALSE immediately
        var evalAfter = _evaluator.Evaluate(flag, "operator@criacerto.com.br", "SupportN2");
        evalAfter.Should().BeFalse();

        // Verify forensic audit record with Critical severity and SHA-256 integrity
        var audit = await _dbContext.AuditLogs
            .FirstOrDefaultAsync(a => a.Action == "KILL_SWITCH_ACTIVATED" && a.Category == AuditCategory.Rollout);

        audit.Should().NotBeNull();
        audit!.Severity.Should().Be(AuditSeverity.Critical);
        audit.RecordHash.Should().NotBeNullOrWhiteSpace();
        audit.AdminUserEmail.Should().Be("secops@criacerto.com.br");
    }

    [Fact]
    public async Task Runbook01_KillSwitchRestoration_ShouldReactivateFeatureAndAuditDeactivation()
    {
        // 1. Arrange: flag under active kill switch
        const string flagKey = "backoffice.feature.impersonation";
        var flag = FeatureFlag.Create(
            key: flagKey,
            name: "Suporte Assistido",
            description: "Impersonação segura",
            category: FeatureFlagCategory.SupportTools,
            initialRing: RolloutRing.Ring2_GeneralAvailability,
            initialPercentage: 100,
            createdBy: "admin@criacerto.com.br"
        ).Value;

        flag.TriggerKillSwitch("Degradação prévia", "admin@criacerto.com.br");
        _dbContext.FeatureFlags.Add(flag);
        await _dbContext.SaveChangesAsync();

        // 2. Act: Post-containment resolution and restoration
        var deactivationHandler = new DeactivateKillSwitchCommandHandler(_dbContext);
        var command = new DeactivateKillSwitchCommand(
            FlagKey: flagKey,
            Reason: "Correção de hotfix v1.2.5 validada em staging com sucesso",
            ActorId: Guid.NewGuid(),
            ActorEmail: "platform_owner@criacerto.com.br",
            ActorRole: "PlatformOwner",
            IpAddress: "10.0.0.1",
            UserAgent: "IncidentResponse-Drill"
        );

        var result = await deactivationHandler.Handle(command, CancellationToken.None);

        // 3. Assert: Flag restored
        result.IsSuccess.Should().BeTrue();
        result.Value.KillSwitchActive.Should().BeFalse();

        var eval = _evaluator.Evaluate(flag, "support@criacerto.com.br", "SupportN2");
        eval.Should().BeTrue();

        var audit = await _dbContext.AuditLogs
            .FirstOrDefaultAsync(a => a.Action == "KILL_SWITCH_DEACTIVATED");
        audit.Should().NotBeNull();
        audit!.Severity.Should().Be(AuditSeverity.High);
    }

    [Fact]
    public async Task Runbook02_ForensicTamperDetection_WhenAuditLogModified_ShouldDetectChainIntegrityViolation()
    {
        // 1. Arrange: Create two valid sequentially hashed logs
        var log1 = AuditLog.Create(Guid.NewGuid(), "admin@criacerto.com.br", "Tenant.Created", "Tenant/1", "127.0.0.1");
        var log2 = AuditLog.Create(Guid.NewGuid(), "admin@criacerto.com.br", "Plan.Published", "Plan/1", "127.0.0.1");

        _dbContext.AuditLogs.AddRange(log1, log2);
        await _dbContext.SaveChangesAsync();

        // 2. Tamper: corrupt the RecordHash of log1 directly in the database
        typeof(AuditLog).GetProperty(nameof(AuditLog.RecordHash))!
            .SetValue(log1, "MALICIOUS_TAMPERED_HASH_VAL_1234567890abcdef");
        await _dbContext.SaveChangesAsync();

        // 3. Act: Run integrity verification query (part of RUNBOOK-02 diagnostics)
        var handler = new VerifyAuditTrailIntegrityQueryHandler(_dbContext);
        var result = await handler.Handle(new VerifyAuditTrailIntegrityQuery(), CancellationToken.None);

        // 4. Assert: Tampering detected
        result.IsSuccess.Should().BeTrue();
        result.Value.IsChainValid.Should().BeFalse();
        result.Value.TamperedRecordsCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task FinancePlaybook_PlanPublishing_EnforcesFourEyesPrinciple_PreventingSelfApproval()
    {
        // 1. Arrange: Seed plan catalog with draft version
        var planCatalog = PlanCatalog.Create("PRO", "Plano Pro", "Plano intermediário para cria e recria", "Cattle").Value;
        planCatalog.CreateVersion("v2.0", 199m, 1990m, 1500);
        var version = planCatalog.Versions.First();
        _dbContext.PlanCatalogs.Add(planCatalog);

        var requesterId = Guid.NewGuid();
        const string requesterEmail = "finance@criacerto.com.br";
        var reviewerId = Guid.NewGuid();
        const string reviewerEmail = "platform_owner@criacerto.com.br";

        var request = AdminApprovalRequest.Create(
            ApprovalRequestType.PublishPlanVersion,
            "Publicação de Plano Pro v2.0",
            "Ajuste de capacidade zootécnica e inclusão de IATF",
            $"PlanVersion/{version.Id}",
            "Impacto direto na contratação de novos clientes",
            $"{{\"VersionId\":\"{version.Id}\",\"ApprovalNotes\":\"Aprovado conforme diretoria\"}}",
            requesterId,
            requesterEmail).Value;

        _dbContext.AdminApprovalRequests.Add(request);
        await _dbContext.SaveChangesAsync();

        var handler = new ApproveApprovalRequestCommandHandler(_dbContext, _sender);

        // 2. Act: FinanceOps attempts self-approval (violating 4-Eyes principle)
        var selfApprovalCommand = new ApproveApprovalRequestCommand(
            request.Id,
            requesterId, // Same ID as requester
            requesterEmail,
            "127.0.0.1",
            "Tentativa irregular de autoaprovação de catálogo");

        var selfApprovalResult = await handler.Handle(selfApprovalCommand, CancellationToken.None);

        // 3. Assert: Self-approval rejected strictly with Result.Failure
        selfApprovalResult.IsFailure.Should().BeTrue();
        selfApprovalResult.Error.Code.Should().Be(ApprovalErrors.CannotSelfApprove.Code);

        // 4. Act: Legitimate review by different operator (PlatformOwner)
        var validApprovalCommand = new ApproveApprovalRequestCommand(
            request.Id,
            reviewerId, // Different reviewer
            reviewerEmail,
            "127.0.0.1",
            "Aprovado conforme deliberação da diretoria comercial");

        var validResult = await handler.Handle(validApprovalCommand, CancellationToken.None);

        // 5. Assert: Legitimate approval succeeds and plan version is published
        validResult.IsSuccess.Should().BeTrue();
        validResult.Value.Status.Should().Be(ApprovalRequestStatus.Executed);

        var approvedRequest = await _dbContext.AdminApprovalRequests.FindAsync(request.Id);
        approvedRequest!.Status.Should().Be(ApprovalRequestStatus.Executed);
        approvedRequest.ReviewedByAdminUserId.Should().Be(reviewerId);

        var updatedPlan = await _dbContext.PlanCatalogs.Include(p => p.Versions).FirstAsync(p => p.Id == planCatalog.Id);
        var publishedVersion = updatedPlan.Versions.First(v => v.Id == version.Id);
        publishedVersion.Status.Should().Be(PlanVersionStatus.Published);
    }

    [Fact]
    public async Task SupportPlaybook_SensitiveDataUnmasking_EnforcesJustificationAndRecordsHighSeverityAudit()
    {
        // 1. Arrange: Setup compliance handler
        var tenantId = Guid.NewGuid();
        var rawCpf = "12345678900";
        var tenantDetail = new TenantBackofficeDetailDto(
            tenantId, "Fazenda Alvorada", "Fazenda Alvorada Ltda", rawCpf, "EXT-01",
            "Active", "Enterprise", 1500, 2000, false, "MS", "Campo Grande", "123456",
            500, "Bovinocultura", "Medium", "Centro-Oeste", "Corte", "Low",
            new List<TenantOperationalTagDto>(), "João Silva", "joao@fazenda.com.br",
            "Maria Santos", "maria@fazenda.com.br", false, null, null, 5, 2,
            DateTime.UtcNow.AddMonths(-6), DateTime.UtcNow
        );

        _sender.Send(Arg.Is<GetTenantBackofficeDetailQuery>(q => q.TenantId == tenantId), Arg.Any<CancellationToken>())
            .Returns(Result.Success(tenantDetail));

        var handler = new RevealSensitiveDataCommandHandler(_dbContext, _sender, _masker);

        // 2. Act & Assert: Reject when justification is too short (< 10 chars)
        var invalidCommand = new RevealSensitiveDataCommand(
            Guid.NewGuid(), "support_n2@criacerto.com.br", "SupportN2", "192.168.1.1", null,
            new RevealSensitiveDataRequest("Tenant", tenantId, "CNPJ", "curto")
        );

        var invalidResult = await handler.Handle(invalidCommand, CancellationToken.None);
        invalidResult.IsFailure.Should().BeTrue();
        invalidResult.Error.Code.Should().Be("Compliance.JustificationRequired");

        // 3. Act & Assert: Accept with valid justification (>= 10 chars) and record high severity audit log
        var validCommand = new RevealSensitiveDataCommand(
            Guid.NewGuid(), "support_n2@criacerto.com.br", "SupportN2", "192.168.1.1", null,
            new RevealSensitiveDataRequest("Tenant", tenantId, "CNPJ", "Validação fiscal conforme ticket SUP-5544 com consentimento")
        );

        var validResult = await handler.Handle(validCommand, CancellationToken.None);
        validResult.IsSuccess.Should().BeTrue();
        validResult.Value.PlainValue.Should().Be(rawCpf);

        var audit = await _dbContext.AuditLogs
            .FirstOrDefaultAsync(a => a.Action == "PII_DATA_UNMASKED");

        audit.Should().NotBeNull();
        audit!.Severity.Should().Be(AuditSeverity.High);
        audit.AdminUserEmail.Should().Be("support_n2@criacerto.com.br");
        audit.VerifyIntegrity().Should().BeTrue();
    }
}
