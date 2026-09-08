using System.Security.Claims;
using CriaCerto.Modules.Backoffice.Application.Security;
using CriaCerto.Web.Client.Services;
using FluentAssertions;
using Xunit;

namespace CriaCerto.Web.Client.UnitTests.Services;

public class BackofficePermissionServiceTests
{
    [Fact]
    public async Task HasPermissionAsync_WhenUserIsPlatformOwner_ShouldReturnTrue()
    {
        // Arrange
        var service = new BackofficePermissionService();
        var claims = new[] { new Claim(ClaimTypes.Role, BackofficeRoles.PlatformOwner) };
        service.SetCurrentUser(new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")));

        // Act
        var result = await service.HasPermissionAsync(BackofficePermissions.TenantsSuspend);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasPermissionAsync_WhenUserIsSupportN1_ShouldAllowReadAndDenySuspend()
    {
        // Arrange
        var service = new BackofficePermissionService();
        var claims = new[] { new Claim(ClaimTypes.Role, BackofficeRoles.SupportN1) };
        service.SetCurrentUser(new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")));

        // Act
        var readResult = await service.HasPermissionAsync(BackofficePermissions.TenantsRead);
        var suspendResult = await service.HasPermissionAsync(BackofficePermissions.TenantsSuspend);

        // Assert
        readResult.Should().BeTrue();
        suspendResult.Should().BeFalse();
    }

    [Fact]
    public async Task HasPermissionAsync_WhenUserIsSupportN1_ShouldAllowDiagnoseAndDenyRemediate()
    {
        // Arrange
        var service = new BackofficePermissionService();
        var claims = new[] { new Claim(ClaimTypes.Role, BackofficeRoles.SupportN1) };
        service.SetCurrentUser(new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")));

        // Act
        var diagnoseResult = await service.HasPermissionAsync(BackofficePermissions.SupportDiagnose);
        var remediateResult = await service.HasPermissionAsync(BackofficePermissions.SupportRemediate);

        // Assert
        diagnoseResult.Should().BeTrue();
        remediateResult.Should().BeFalse();
    }

    [Fact]
    public async Task HasPermissionAsync_WhenUserIsSupportN2_ShouldAllowDiagnoseAndRemediate()
    {
        // Arrange
        var service = new BackofficePermissionService();
        var claims = new[] { new Claim(ClaimTypes.Role, BackofficeRoles.SupportN2) };
        service.SetCurrentUser(new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")));

        // Act
        var diagnoseResult = await service.HasPermissionAsync(BackofficePermissions.SupportDiagnose);
        var remediateResult = await service.HasPermissionAsync(BackofficePermissions.SupportRemediate);

        // Assert
        diagnoseResult.Should().BeTrue();
        remediateResult.Should().BeTrue();
    }

    [Fact]
    public async Task HasPermissionAsync_WhenUserIsSupportN2_ShouldAllowApprovalsRequestAndDenyApprovalsReview()
    {
        // Arrange
        var service = new BackofficePermissionService();
        var claims = new[] { new Claim(ClaimTypes.Role, BackofficeRoles.SupportN2) };
        service.SetCurrentUser(new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")));

        // Act
        var requestResult = await service.HasPermissionAsync(BackofficePermissions.ApprovalsRequest);
        var reviewResult = await service.HasPermissionAsync(BackofficePermissions.ApprovalsReview);

        // Assert
        requestResult.Should().BeTrue();
        reviewResult.Should().BeFalse();
    }

    [Fact]
    public async Task HasPermissionAsync_WhenUserIsPlatformOwner_ShouldAllowApprovalsRequestAndReview()
    {
        // Arrange
        var service = new BackofficePermissionService();
        var claims = new[] { new Claim(ClaimTypes.Role, BackofficeRoles.PlatformOwner) };
        service.SetCurrentUser(new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")));

        // Act
        var requestResult = await service.HasPermissionAsync(BackofficePermissions.ApprovalsRequest);
        var reviewResult = await service.HasPermissionAsync(BackofficePermissions.ApprovalsReview);

        // Assert
        requestResult.Should().BeTrue();
        reviewResult.Should().BeTrue();
    }

    [Fact]
    public async Task HasPermissionAsync_WhenUserIsSupportN1_ShouldAllowObservabilityReadAndDenyManage()
    {
        // Arrange
        var service = new BackofficePermissionService();
        var claims = new[] { new Claim(ClaimTypes.Role, BackofficeRoles.SupportN1) };
        service.SetCurrentUser(new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")));

        // Act
        var readResult = await service.HasPermissionAsync(BackofficePermissions.ObservabilityRead);
        var manageResult = await service.HasPermissionAsync(BackofficePermissions.ObservabilityManage);

        // Assert
        readResult.Should().BeTrue();
        manageResult.Should().BeFalse();
    }

    [Fact]
    public async Task HasPermissionAsync_WhenUserIsSupportN2_ShouldAllowObservabilityReadAndManage()
    {
        // Arrange
        var service = new BackofficePermissionService();
        var claims = new[] { new Claim(ClaimTypes.Role, BackofficeRoles.SupportN2) };
        service.SetCurrentUser(new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")));

        // Act
        var readResult = await service.HasPermissionAsync(BackofficePermissions.ObservabilityRead);
        var manageResult = await service.HasPermissionAsync(BackofficePermissions.ObservabilityManage);

        // Assert
        readResult.Should().BeTrue();
        manageResult.Should().BeTrue();
    }

    [Fact]
    public async Task HasPermissionAsync_WhenUserIsReadOnlyAuditor_ShouldAllowComplianceReadAndExport_AndDenyUnmask()
    {
        // Arrange
        var service = new BackofficePermissionService();
        var claims = new[] { new Claim(ClaimTypes.Role, BackofficeRoles.ReadOnlyAuditor) };
        service.SetCurrentUser(new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")));

        // Act
        var readResult = await service.HasPermissionAsync(BackofficePermissions.ComplianceRead);
        var exportResult = await service.HasPermissionAsync(BackofficePermissions.ComplianceExport);
        var unmaskResult = await service.HasPermissionAsync(BackofficePermissions.ComplianceUnmask);

        // Assert
        readResult.Should().BeTrue();
        exportResult.Should().BeTrue();
        unmaskResult.Should().BeFalse();
    }

    [Fact]
    public async Task HasPermissionAsync_WhenUserIsSupportN1_ShouldDenyCompliancePermissions()
    {
        // Arrange
        var service = new BackofficePermissionService();
        var claims = new[] { new Claim(ClaimTypes.Role, BackofficeRoles.SupportN1) };
        service.SetCurrentUser(new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")));

        // Act
        var readResult = await service.HasPermissionAsync(BackofficePermissions.ComplianceRead);
        var unmaskResult = await service.HasPermissionAsync(BackofficePermissions.ComplianceUnmask);

        // Assert
        readResult.Should().BeFalse();
        unmaskResult.Should().BeFalse();
    }

    [Fact]
    public async Task HasPermissionAsync_WhenUserIsPlatformOwner_ShouldAllowAllCompliancePermissions()
    {
        // Arrange
        var service = new BackofficePermissionService();
        var claims = new[] { new Claim(ClaimTypes.Role, BackofficeRoles.PlatformOwner) };
        service.SetCurrentUser(new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")));

        // Act
        var readResult = await service.HasPermissionAsync(BackofficePermissions.ComplianceRead);
        var exportResult = await service.HasPermissionAsync(BackofficePermissions.ComplianceExport);
        var unmaskResult = await service.HasPermissionAsync(BackofficePermissions.ComplianceUnmask);

        // Assert
        readResult.Should().BeTrue();
        exportResult.Should().BeTrue();
        unmaskResult.Should().BeTrue();
    }
}
