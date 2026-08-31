using System.Security.Claims;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Backoffice.Application.Domain.Entities;
using CriaCerto.Modules.Backoffice.Application.Domain.Errors;
using CriaCerto.Modules.Backoffice.Application.Features.Approvals.Commands;
using CriaCerto.Modules.Backoffice.Application.Security;
using CriaCerto.Modules.Backoffice.Infrastructure.Persistence;
using CriaCerto.Modules.Backoffice.Infrastructure.Security;
using FluentAssertions;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace CriaCerto.Modules.Backoffice.UnitTests.Security;

[Trait("Category", "SecurityRegression")]
public class BackofficeNegativeAuthorizationTests
{
    private readonly PermissionEvaluatorService _evaluator = new();

    private ClaimsPrincipal CreatePrincipalForRole(string role, string? scope = BackofficePermissions.ScopeGlobal)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Email, $"{role.ToLowerInvariant()}@criacerto.com.br"),
            new(ClaimTypes.Role, role),
            new("role", role),
            new("is_backoffice_admin", "true")
        };

        if (!string.IsNullOrWhiteSpace(scope))
        {
            claims.Add(new("scope", scope));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "BackofficeAuthScheme"));
    }

    [Theory]
    [InlineData(BackofficePermissions.TenantsWrite)]
    [InlineData(BackofficePermissions.TenantsSuspend)]
    [InlineData(BackofficePermissions.PlansWrite)]
    [InlineData(BackofficePermissions.PlansPublish)]
    [InlineData(BackofficePermissions.ImpersonationStart)]
    [InlineData(BackofficePermissions.SupportRemediate)]
    [InlineData(BackofficePermissions.ApprovalsRequest)]
    [InlineData(BackofficePermissions.ApprovalsReview)]
    [InlineData(BackofficePermissions.UsersAdminManage)]
    [InlineData(BackofficePermissions.ComplianceUnmask)]
    [InlineData(BackofficePermissions.ObservabilityManage)]
    public void ReadOnlyAuditor_ShouldBeDenied_ForAnyMutatingOrPrivilegedPermission(string forbiddenPermission)
    {
        // Arrange
        var auditor = CreatePrincipalForRole(BackofficeRoles.ReadOnlyAuditor);

        // Act
        var result = _evaluator.HasPermission(auditor, forbiddenPermission, BackofficePermissions.ScopeGlobal);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse(
            $"ReadOnlyAuditor deve sofrer negação estrita para a permissão '{forbiddenPermission}'");
    }

    [Theory]
    [InlineData(BackofficePermissions.TenantsSuspend)]
    [InlineData(BackofficePermissions.TenantsWrite)]
    [InlineData(BackofficePermissions.PlansPublish)]
    [InlineData(BackofficePermissions.PlansWrite)]
    [InlineData(BackofficePermissions.ImpersonationStart)]
    [InlineData(BackofficePermissions.SupportRemediate)]
    [InlineData(BackofficePermissions.ApprovalsRequest)]
    [InlineData(BackofficePermissions.ApprovalsReview)]
    [InlineData(BackofficePermissions.UsersAdminManage)]
    [InlineData(BackofficePermissions.ComplianceUnmask)]
    [InlineData(BackofficePermissions.ComplianceExport)]
    [InlineData(BackofficePermissions.ObservabilityManage)]
    public void SupportN1_ShouldBeDenied_ForAdvancedActionsAndUnmasking(string forbiddenPermission)
    {
        // Arrange
        var supportN1 = CreatePrincipalForRole(BackofficeRoles.SupportN1);

        // Act
        var result = _evaluator.HasPermission(supportN1, forbiddenPermission, BackofficePermissions.ScopeGlobal);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse(
            $"SupportN1 não pode ter acesso à permissão crítica '{forbiddenPermission}'");
    }

    [Theory]
    [InlineData(BackofficePermissions.PlansPublish)]
    [InlineData(BackofficePermissions.ApprovalsReview)]
    [InlineData(BackofficePermissions.UsersAdminManage)]
    [InlineData(BackofficePermissions.ComplianceUnmask)]
    [InlineData(BackofficePermissions.ComplianceExport)]
    public void SupportN2_ShouldBeDenied_ForPlanPublishingAndApprovalReview(string forbiddenPermission)
    {
        // Arrange
        var supportN2 = CreatePrincipalForRole(BackofficeRoles.SupportN2);

        // Act
        var result = _evaluator.HasPermission(supportN2, forbiddenPermission, BackofficePermissions.ScopeGlobal);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse(
            $"SupportN2 não deve possuir a permissão restrita '{forbiddenPermission}'");
    }

    [Theory]
    [InlineData(BackofficePermissions.ImpersonationStart)]
    [InlineData(BackofficePermissions.ImpersonationStop)]
    [InlineData(BackofficePermissions.SupportRemediate)]
    [InlineData(BackofficePermissions.ApprovalsReview)]
    [InlineData(BackofficePermissions.UsersAdminManage)]
    [InlineData(BackofficePermissions.ComplianceUnmask)]
    [InlineData(BackofficePermissions.ComplianceExport)]
    [InlineData(BackofficePermissions.ObservabilityManage)]
    public void FinanceOps_ShouldBeDenied_ForTechnicalRemediationAndImpersonation(string forbiddenPermission)
    {
        // Arrange
        var financeOps = CreatePrincipalForRole(BackofficeRoles.FinanceOps);

        // Act
        var result = _evaluator.HasPermission(financeOps, forbiddenPermission, BackofficePermissions.ScopeGlobal);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse(
            $"FinanceOps não deve possuir a permissão não-financeira '{forbiddenPermission}'");
    }

    [Fact]
    public void AnonymousOrEmptyUser_ShouldBeDenied_ForAnyPermission()
    {
        // Arrange
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        var result = _evaluator.HasPermission(anonymous, BackofficePermissions.TenantsRead, BackofficePermissions.ScopeGlobal);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse("Usuário não autenticado deve ter negação padrão (Default Deny).");
    }

    [Fact]
    public void UserWithInvalidScope_ShouldFailScopeValidation()
    {
        // Arrange
        var user = CreatePrincipalForRole(BackofficeRoles.SupportN1);

        // Act
        var result = _evaluator.HasPermission(user, BackofficePermissions.TenantsRead, "InvalidScopeXYZ");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(BackofficeErrors.InvalidScopeData.Code);
    }

    [Fact]
    public async Task FourEyesApproval_WhenRequesterAttemptsSelfApproval_ShouldStrictlyFailWithCannotSelfApprove()
    {
        // Arrange
        using var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        var dbOptions = new DbContextOptionsBuilder<BackofficeDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new BackofficeDbContext(dbOptions);
        context.Database.EnsureCreated();

        var adminId = Guid.NewGuid();
        var adminEmail = "requester@criacerto.com.br";

        var approvalRequest = AdminApprovalRequest.Create(
            ApprovalRequestType.PublishPlanVersion,
            "PlanoEnterpriseV2",
            "Solicitação de publicação de nova versão do plano enterprise",
            "PlanVersion/10",
            "Impacto direto nas novas assinaturas",
            "{\"VersionId\":\"10\"}",
            adminId,
            adminEmail).Value;

        context.AdminApprovalRequests.Add(approvalRequest);
        await context.SaveChangesAsync();

        var sender = Substitute.For<ISender>();
        var handler = new ApproveApprovalRequestCommandHandler(context, sender);

        // Act: The exact same requester attempts to approve their own request (BFLA/Tampering)
        var approveCmd = new ApproveApprovalRequestCommand(
            approvalRequest.Id,
            ReviewedByAdminUserId: adminId, // Self-approval attempt!
            ReviewedByAdminEmail: adminEmail,
            IpAddress: "127.0.0.1",
            ReviewNotes: "Aprovando minha própria solicitação de plano.");

        var result = await handler.Handle(approveCmd, CancellationToken.None);

        // Assert: 4-Eyes Principle enforces strict failure
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ApprovalErrors.CannotSelfApprove.Code);
    }
}
