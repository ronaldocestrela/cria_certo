using System.Security.Claims;
using CriaCerto.Modules.Backoffice.Application.Security;
using CriaCerto.Modules.Backoffice.Infrastructure.Security;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace CriaCerto.Modules.Backoffice.UnitTests.Security;

public class PermissionAuthorizationHandlerTests
{
    private readonly PermissionEvaluatorService _evaluator = new();

    [Fact]
    public async Task HandleAsync_WhenUserIsPlatformOwner_ShouldSucceedForAnyPermission()
    {
        // Arrange
        var handler = new PermissionAuthorizationHandler(_evaluator);
        var requirement = new PermissionRequirement(BackofficePermissions.TenantsSuspend);

        var claims = new[]
        {
            new Claim(ClaimTypes.Role, BackofficeRoles.PlatformOwner)
        };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);

        // Act
        await handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_WhenUserLacksRequiredPermission_ShouldNotSucceed()
    {
        // Arrange
        var handler = new PermissionAuthorizationHandler(_evaluator);
        var requirement = new PermissionRequirement(BackofficePermissions.TenantsSuspend);

        var claims = new[]
        {
            new Claim(ClaimTypes.Role, BackofficeRoles.SupportN1)
        };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);

        // Act
        await handler.HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
    }
}
