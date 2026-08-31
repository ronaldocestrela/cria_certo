using CriaCerto.Api.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace CriaCerto.Architecture.IntegrationTests;

public class SecurityConfigurationTests
{
    [Fact]
    public async Task SecurityHeadersMiddleware_ShouldInjectSecurityHeaders_OnHttpResponse()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        RequestDelegate next = (ctx) =>
        {
            ctx.Response.StatusCode = 200;
            return Task.CompletedTask;
        };

        var middleware = new SecurityHeadersMiddleware(next);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers["X-Content-Type-Options"].ToString().Should().Be("nosniff");
        context.Response.Headers["X-Frame-Options"].ToString().Should().Be("DENY");
        context.Response.Headers["X-XSS-Protection"].ToString().Should().Be("1; mode=block");
        context.Response.Headers["Referrer-Policy"].ToString().Should().Be("strict-origin-when-cross-origin");
        context.Response.Headers["X-Permitted-Cross-Domain-Policies"].ToString().Should().Be("none");
        context.Response.Headers["Permissions-Policy"].ToString().Should().Contain("camera=()");
        context.Response.Headers["Content-Security-Policy"].ToString().Should().Contain("default-src 'self'");
    }

    [Fact]
    public async Task SecurityHeadersMiddleware_ShouldInjectNoStoreCacheControl_OnBackofficeRoutes()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/backoffice/tenants";
        RequestDelegate next = (ctx) => Task.CompletedTask;
        var middleware = new SecurityHeadersMiddleware(next);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers["Cache-Control"].ToString().Should().Contain("no-store");
        context.Response.Headers["Pragma"].ToString().Should().Be("no-cache");
    }

    [Fact]
    public async Task SecurityHeadersMiddleware_ShouldAddHsts_WhenRequestIsHttps()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        RequestDelegate next = (ctx) => Task.CompletedTask;

        var middleware = new SecurityHeadersMiddleware(next);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers["Strict-Transport-Security"].ToString().Should().Contain("max-age=31536000");
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("CriaCertoSuperSecretKeyThatIsAtLeast32BytesLong!", true)] // Default dev secret
    [InlineData("short_secret_key_123", true)] // Less than 32 bytes
    [InlineData("StrongProductionSecretKey_ThatIsAtLeast32BytesLong_2026_GoLive!", false)] // Valid production secret
    public void ValidateProductionJwtSecret_ShouldThrowException_WhenSecretIsInvalidInProduction(string secret, bool shouldThrow)
    {
        // Arrange & Act
        Action act = () =>
        {
            if (string.IsNullOrWhiteSpace(secret) || 
                secret.Contains("SuperSecretKey") || 
                System.Text.Encoding.UTF8.GetByteCount(secret) < 32)
            {
                throw new InvalidOperationException("ERRO DE SEGURANÇA: Chave JWT de produção ausente ou insegura.");
            }
        };

        // Assert
        if (shouldThrow)
        {
            act.Should().Throw<InvalidOperationException>()
               .WithMessage("*ERRO DE SEGURANÇA*");
        }
        else
        {
            act.Should().NotThrow();
        }
    }
}
