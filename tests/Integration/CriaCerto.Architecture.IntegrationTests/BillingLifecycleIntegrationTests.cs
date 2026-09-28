using CriaCerto.Api.Middleware;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.BuildingBlocks.Abstractions.Tenancy;
using CriaCerto.Modules.Tenancy.Application.Abstractions;
using CriaCerto.Modules.Tenancy.Application.Domain.Errors;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using System.Text.Json;

namespace CriaCerto.Architecture.IntegrationTests;

public class BillingLifecycleIntegrationTests
{
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly ITenantAccessGuard _tenantAccessGuard = Substitute.For<ITenantAccessGuard>();

    [Fact]
    public async Task TenantAccessMiddleware_Should_AllowBypass_For_BillingAndProfileEndpoints_EvenIfTrialExpired()
    {
        // Arrange
        var nextInvoked = false;
        RequestDelegate next = (ctx) =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        };

        var middleware = new TenantAccessMiddleware(next);
        var tenantId = Guid.NewGuid();
        _tenantContext.TenantId.Returns(tenantId);

        var bypassPaths = new[]
        {
            "/api/v1/tenancy/plans",
            "/api/v1/tenancy/profile",
            "/api/v1/tenancy/subscription/checkout",
            "/api/v1/tenancy/subscription/portal",
            "/api/v1/payments/stripe-webhook",
            "/api/auth/login",
            "/api/v1/auth/refresh-token"
        };

        foreach (var path in bypassPaths)
        {
            nextInvoked = false;
            var context = new DefaultHttpContext();
            context.Request.Path = path;

            // Act
            await middleware.InvokeAsync(context, _tenantContext, _tenantAccessGuard);

            // Assert
            nextInvoked.Should().BeTrue($"path {path} should bypass tenant access guard");
            await _tenantAccessGuard.DidNotReceiveWithAnyArgs().EnsureProducerAccessAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        }
    }

    [Fact]
    public async Task TenantAccessMiddleware_Should_Block_OperationalRoute_When_TrialExpired()
    {
        // Arrange
        var nextInvoked = false;
        RequestDelegate next = (ctx) =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        };

        var middleware = new TenantAccessMiddleware(next);
        var tenantId = Guid.NewGuid();
        _tenantContext.TenantId.Returns(tenantId);

        _tenantAccessGuard.EnsureProducerAccessAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(Result.Failure(TenancyErrors.TrialExpired));

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/breeding/cows";
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context, _tenantContext, _tenantAccessGuard);

        // Assert
        nextInvoked.Should().BeFalse("Operational routes should be blocked when trial is expired");
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var responseBody = await reader.ReadToEndAsync();
        responseBody.Should().Contain(TenancyErrors.TrialExpired.Code);
    }

    [Fact]
    public async Task TenantAccessMiddleware_Should_Allow_OperationalRoute_When_AccessIsAllowed()
    {
        // Arrange
        var nextInvoked = false;
        RequestDelegate next = (ctx) =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        };

        var middleware = new TenantAccessMiddleware(next);
        var tenantId = Guid.NewGuid();
        _tenantContext.TenantId.Returns(tenantId);

        _tenantAccessGuard.EnsureProducerAccessAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/breeding/cows";

        // Act
        await middleware.InvokeAsync(context, _tenantContext, _tenantAccessGuard);

        // Assert
        nextInvoked.Should().BeTrue();
    }
}
