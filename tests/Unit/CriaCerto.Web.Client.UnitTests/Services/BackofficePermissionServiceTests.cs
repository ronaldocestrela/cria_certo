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
}
