using System.Security.Claims;
using CriaCerto.Modules.Backoffice.Infrastructure.Security;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace CriaCerto.Modules.Backoffice.UnitTests.Security;

public class BackofficeAccessMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenRouteIsBackofficeAndUserIsNotAdmin_ShouldBlockWith401Unauthorized()
    {
        // Arrange
        var middleware = new BackofficeAccessMiddleware(next: (innerHttpContext) => Task.CompletedTask);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/backoffice/dashboard/kpis";

        // Act
        await middleware.InvokeAsync(context);

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
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().NotBe(StatusCodes.Status401Unauthorized);
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
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
    }
}
