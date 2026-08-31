using System.Security.Claims;
using CriaCerto.Modules.Backoffice.Infrastructure.Security;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace CriaCerto.Modules.Backoffice.UnitTests.Security;

public class BackofficeAccessMiddlewareTests
{
    private readonly PermissionEvaluatorService _evaluator = new();

    [Fact]
    public async Task InvokeAsync_WhenRouteIsBackofficeAndUserIsNotAdmin_ShouldBlockWith401Unauthorized()
    {
        // Arrange
        var middleware = new BackofficeAccessMiddleware(next: (innerHttpContext) => Task.CompletedTask);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/backoffice/dashboard/kpis";

        // Act
        await middleware.InvokeAsync(context, _evaluator);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task InvokeAsync_WhenRouteIsBackofficeAndUserHasAdminClaim_ShouldAllowAndCallNext()
    {
        // Arrange
        var nextCalled = false;
        var middleware = new BackofficeAccessMiddleware(next: (innerHttpContext) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/backoffice/dashboard/kpis";

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, "PlatformOwner"),
            new Claim("is_backoffice_admin", "true")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        context.User = new ClaimsPrincipal(identity);

        // Act
        await middleware.InvokeAsync(context, _evaluator);

        // Assert
        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().NotBe(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task InvokeAsync_WhenUserUsesImpersonationToken_ShouldBlockWith403Forbidden()
    {
        // Arrange
        var nextCalled = false;
        var middleware = new BackofficeAccessMiddleware(next: _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/backoffice/dashboard/kpis";

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim("is_impersonation", "true"),
            new Claim("TenantId", Guid.NewGuid().ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        context.User = new ClaimsPrincipal(identity);

        // Act
        await middleware.InvokeAsync(context, _evaluator);

        // Assert
        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task InvokeAsync_WhenUserIsAuthenticatedButNotAdmin_ShouldBlockWith403Forbidden()
    {
        // Arrange
        var nextCalled = false;
        var middleware = new BackofficeAccessMiddleware(next: _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/backoffice/dashboard/kpis";

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, "ProducerUser"),
            new Claim("TenantId", Guid.NewGuid().ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        context.User = new ClaimsPrincipal(identity);

        // Act
        await middleware.InvokeAsync(context, _evaluator);

        // Assert
        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task InvokeAsync_WhenRouteIsBackofficeAuthLogin_ShouldBypassMiddleware()
    {
        // Arrange
        var nextCalled = false;
        var middleware = new BackofficeAccessMiddleware(next: _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/backoffice/auth/login";

        // Act
        await middleware.InvokeAsync(context, _evaluator);

        // Assert
        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task InvokeAsync_WhenRouteIsBackofficeAuthRefresh_ShouldBypassMiddleware()
    {
        // Arrange
        var nextCalled = false;
        var middleware = new BackofficeAccessMiddleware(next: _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/backoffice/auth/refresh";

        // Act
        await middleware.InvokeAsync(context, _evaluator);

        // Assert
        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task InvokeAsync_WhenRouteIsNotBackoffice_ShouldBypassMiddleware()
    {
        // Arrange
        var nextCalled = false;
        var middleware = new BackofficeAccessMiddleware(next: (innerHttpContext) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/tenancy/farms";

        // Act
        await middleware.InvokeAsync(context, _evaluator);

        // Assert
        nextCalled.Should().BeTrue();
    }
}
