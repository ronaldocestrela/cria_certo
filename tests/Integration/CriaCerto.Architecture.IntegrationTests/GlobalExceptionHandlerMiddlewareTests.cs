using System.IO;
using System.Text.Json;
using CriaCerto.Api.Middleware;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.BuildingBlocks.Infrastructure.Security;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CriaCerto.Architecture.IntegrationTests;

public class GlobalExceptionHandlerMiddlewareTests
{
    private readonly PiiRedactor _redactor = new();
    private readonly NullLogger<GlobalExceptionHandlerMiddleware> _logger = new();

    [Fact]
    public async Task InvokeAsync_ShouldCatchUnhandledException_AndReturnStandardized500ErrorPayload()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.TraceIdentifier = "req_test_trace_12345";

        RequestDelegate next = (ctx) =>
        {
            throw new InvalidOperationException("Fatal database crash: Server=127.0.0.1;User Id=sa;Password=SensitiveSuperSecret123!; at Microsoft.Data.SqlClient.SqlConnection.Open()");
        };

        var middleware = new GlobalExceptionHandlerMiddleware(next, _logger, _redactor);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        context.Response.ContentType.Should().Contain("application/json");

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var responseBody = await reader.ReadToEndAsync();

        // Must not expose stack traces, connection strings or raw exceptions to HTTP client
        responseBody.Should().NotContain("SensitiveSuperSecret123!");
        responseBody.Should().NotContain("Fatal database crash");
        responseBody.Should().NotContain("SqlConnection.Open()");

        // Must return standard Result Pattern Error
        responseBody.Should().Contain("\"code\":\"Server.UnexpectedError\"");
        responseBody.Should().Contain("req_test_trace_12345");
        responseBody.Should().Contain("\"type\":0"); // ErrorType.Failure
    }

    [Fact]
    public async Task InvokeAsync_ShouldPassThrough_WhenNoExceptionIsThrown()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        RequestDelegate next = (ctx) =>
        {
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            return ctx.Response.WriteAsync("OK");
        };

        var middleware = new GlobalExceptionHandlerMiddleware(next, _logger, _redactor);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }
}
