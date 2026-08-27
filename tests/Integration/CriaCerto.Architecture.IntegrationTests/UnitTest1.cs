using CriaCerto.BuildingBlocks.Abstractions.Tenancy;
using CriaCerto.BuildingBlocks.Infrastructure.Tenancy;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace CriaCerto.Architecture.IntegrationTests;

public class TenancyIntegrationTests
{
    [Fact]
    public void HttpContextTenantContext_ShouldResolveTenantIdFromClaims()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddHttpContextAccessor();
        services.AddScoped<ITenantContext, HttpContextTenantContext>();

        var serviceProvider = services.BuildServiceProvider();
        var httpContextAccessor = serviceProvider.GetRequiredService<IHttpContextAccessor>();
        var tenantContext = serviceProvider.GetRequiredService<ITenantContext>();

        var tenantId = Guid.NewGuid();
        var claims = new[] { new Claim("TenantId", tenantId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };
        httpContextAccessor.HttpContext = httpContext;

        // Act
        var resolvedTenantId = tenantContext.TenantId;

        // Assert
        resolvedTenantId.Should().Be(tenantId);
    }
}
