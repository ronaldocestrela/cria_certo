using System.IO;
using System.Text;
using CriaCerto.Api.Middleware;
using CriaCerto.BuildingBlocks.Infrastructure.Security;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CriaCerto.Architecture.IntegrationTests;

public class RequestLoggingMiddlewareTests
{
    private readonly PiiRedactor _redactor = new();
    private readonly TestLogger<RequestLoggingMiddleware> _logger = new();

    [Fact]
    public async Task InvokeAsync_ShouldCompletelyOmitBody_OnSensitiveEndpoints()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/auth/login";
        context.Request.Method = "POST";
        context.Request.ContentType = "application/json";

        var sensitiveBody = "{\"email\":\"admin@criacerto.com.br\",\"password\":\"SecretPassword123!\"}";
        var bodyBytes = Encoding.UTF8.GetBytes(sensitiveBody);
        context.Request.Body = new MemoryStream(bodyBytes);
        context.Request.ContentLength = bodyBytes.Length;

        RequestDelegate next = (ctx) =>
        {
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        };

        var middleware = new RequestLoggingMiddleware(next, _logger, _redactor);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        _logger.Logs.Should().NotBeEmpty();
        var loggedMessage = _logger.Logs[0];

        loggedMessage.Should().Contain("[REDACTED_SENSITIVE_ENDPOINT]");
        loggedMessage.Should().NotContain("SecretPassword123!");
    }

    [Fact]
    public async Task InvokeAsync_ShouldRedactPiiInBody_OnStandardJsonEndpoints()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/tenants/update";
        context.Request.Method = "PUT";
        context.Request.ContentType = "application/json";

        var jsonBody = """
        {
            "producerName": "Carlos Silva",
            "cpf": "12345678901",
            "pixKey": "carlos@fazenda.com",
            "password": "PasswordToMask123"
        }
        """;
        var bodyBytes = Encoding.UTF8.GetBytes(jsonBody);
        context.Request.Body = new MemoryStream(bodyBytes);
        context.Request.ContentLength = bodyBytes.Length;

        RequestDelegate next = (ctx) =>
        {
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        };

        var middleware = new RequestLoggingMiddleware(next, _logger, _redactor);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        _logger.Logs.Should().NotBeEmpty();
        var loggedMessage = _logger.Logs[0];

        loggedMessage.Should().NotContain("PasswordToMask123");
        loggedMessage.Should().NotContain("12345678901");
        loggedMessage.Should().Contain("***.456.789-**"); // CPF mascarado
        loggedMessage.Should().Contain("[REDACTED]"); // Password e PixKey redigidos
    }

    [Fact]
    public async Task InvokeAsync_ShouldRedactSensitiveQueryStringParameters()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/confirm-email";
        context.Request.Method = "GET";
        context.Request.QueryString = new QueryString("?token=sensitive_jwt_token_12345&email=joao@fazenda.com");

        RequestDelegate next = (ctx) =>
        {
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        };

        var middleware = new RequestLoggingMiddleware(next, _logger, _redactor);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        _logger.Logs.Should().NotBeEmpty();
        var loggedMessage = _logger.Logs[0];

        loggedMessage.Should().NotContain("sensitive_jwt_token_12345");
        loggedMessage.Should().Contain("token=[REDACTED]");
    }

    [Fact]
    public async Task InvokeAsync_ShouldSkipLogging_OnHealthCheckEndpoint()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/health";
        context.Request.Method = "GET";

        RequestDelegate next = (ctx) =>
        {
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        };

        var middleware = new RequestLoggingMiddleware(next, _logger, _redactor);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        _logger.Logs.Should().BeEmpty();
    }

    private class TestLogger<T> : ILogger<T>
    {
        public List<string> Logs { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Logs.Add(formatter(state, exception));
        }
    }
}
