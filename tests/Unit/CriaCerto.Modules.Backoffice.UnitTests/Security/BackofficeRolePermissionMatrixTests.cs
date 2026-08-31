using System.Security.Claims;
using CriaCerto.Modules.Backoffice.Application.Security;
using CriaCerto.Modules.Backoffice.Infrastructure.Security;
using FluentAssertions;
using Xunit;

namespace CriaCerto.Modules.Backoffice.UnitTests.Security;

public class BackofficeRolePermissionMatrixTests
{
    private readonly PermissionEvaluatorService _evaluator = new();

    [Theory]
    [InlineData(BackofficeRoles.PlatformOwner, BackofficePermissions.TenantsRead, true)]
    [InlineData(BackofficeRoles.PlatformOwner, BackofficePermissions.TenantsSuspend, true)]
    [InlineData(BackofficeRoles.PlatformOwner, BackofficePermissions.PlansPublish, true)]
    [InlineData(BackofficeRoles.PlatformOwner, BackofficePermissions.ImpersonationStart, true)]
    [InlineData(BackofficeRoles.PlatformOwner, BackofficePermissions.SupportDiagnose, true)]
    [InlineData(BackofficeRoles.PlatformOwner, BackofficePermissions.SupportRemediate, true)]
    [InlineData(BackofficeRoles.SupportN1, BackofficePermissions.TenantsRead, true)]
    [InlineData(BackofficeRoles.SupportN1, BackofficePermissions.SubscriptionsRead, true)]
    [InlineData(BackofficeRoles.SupportN1, BackofficePermissions.SupportDiagnose, true)]
    [InlineData(BackofficeRoles.SupportN1, BackofficePermissions.SupportRemediate, false)]
    [InlineData(BackofficeRoles.SupportN1, BackofficePermissions.TenantsSuspend, false)]
    [InlineData(BackofficeRoles.SupportN1, BackofficePermissions.PlansPublish, false)]
    [InlineData(BackofficeRoles.SupportN2, BackofficePermissions.TenantsWrite, true)]
    [InlineData(BackofficeRoles.SupportN2, BackofficePermissions.ImpersonationStart, true)]
    [InlineData(BackofficeRoles.SupportN2, BackofficePermissions.SupportDiagnose, true)]
    [InlineData(BackofficeRoles.SupportN2, BackofficePermissions.SupportRemediate, true)]
    [InlineData(BackofficeRoles.SupportN2, BackofficePermissions.PlansPublish, false)]
    [InlineData(BackofficeRoles.FinanceOps, BackofficePermissions.PlansPublish, true)]
    [InlineData(BackofficeRoles.FinanceOps, BackofficePermissions.SubscriptionsManage, true)]
    [InlineData(BackofficeRoles.FinanceOps, BackofficePermissions.ImpersonationStart, false)]
    [InlineData(BackofficeRoles.FinanceOps, BackofficePermissions.SupportRemediate, false)]
    [InlineData(BackofficeRoles.PlatformOwner, BackofficePermissions.ApprovalsRequest, true)]
    [InlineData(BackofficeRoles.PlatformOwner, BackofficePermissions.ApprovalsReview, true)]
    [InlineData(BackofficeRoles.SupportN1, BackofficePermissions.ApprovalsRequest, false)]
    [InlineData(BackofficeRoles.SupportN1, BackofficePermissions.ApprovalsReview, false)]
    [InlineData(BackofficeRoles.SupportN2, BackofficePermissions.ApprovalsRequest, true)]
    [InlineData(BackofficeRoles.SupportN2, BackofficePermissions.ApprovalsReview, false)]
    [InlineData(BackofficeRoles.FinanceOps, BackofficePermissions.ApprovalsRequest, true)]
    [InlineData(BackofficeRoles.FinanceOps, BackofficePermissions.ApprovalsReview, false)]
    [InlineData(BackofficeRoles.ReadOnlyAuditor, BackofficePermissions.ApprovalsRequest, false)]
    [InlineData(BackofficeRoles.ReadOnlyAuditor, BackofficePermissions.ApprovalsReview, false)]
    [InlineData(BackofficeRoles.ReadOnlyAuditor, BackofficePermissions.AuditRead, true)]
    [InlineData(BackofficeRoles.ReadOnlyAuditor, BackofficePermissions.TenantsRead, true)]
    [InlineData(BackofficeRoles.ReadOnlyAuditor, BackofficePermissions.SupportRemediate, false)]
    [InlineData(BackofficeRoles.ReadOnlyAuditor, BackofficePermissions.TenantsWrite, false)]
    public void HasPermission_RolePermissionMatrix_ShouldEvaluateCorrectly(string role, string permission, bool expectedResult)
    {
        // Arrange
        var claims = new[] { new Claim(ClaimTypes.Role, role) };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        // Act
        var result = _evaluator.HasPermission(user, permission, BackofficePermissions.ScopeGlobal);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expectedResult);
    }
}
