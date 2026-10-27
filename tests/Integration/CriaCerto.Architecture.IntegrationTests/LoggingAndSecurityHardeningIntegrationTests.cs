using System.Diagnostics;
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

public class LoggingAndSecurityHardeningIntegrationTests
{
    [Fact]
    public void ErrorUnexpected_ShouldGenerateConsistentPayload_WithTraceId()
    {
        // Arrange
        var traceId = "trace_abc_98765";

        // Act
        var error = Error.Unexpected(traceId);

        // Assert
        error.Code.Should().Be("Server.UnexpectedError");
        error.Type.Should().Be(ErrorType.Failure);
        error.Message.Should().Contain(traceId);
        error.Message.Should().NotContain("Exception");
        error.Message.Should().NotContain("Stack");
    }

    [Fact]
    public async Task CompleteMiddlewarePipeline_ShouldInterceptException_AndNotLeakSensitiveData()
    {
        // Arrange
        var redactor = new PiiRedactor();
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.TraceIdentifier = "e2e_trace_id_456";

        RequestDelegate failingEndpoint = (ctx) =>
        {
            throw new TimeoutException("Database connection timeout: Server=prod-sql;User Id=sa;Password=SuperSecret123!; at System.Threading.Tasks.Task.Wait()");
        };

        var exceptionMiddleware = new GlobalExceptionHandlerMiddleware(
            failingEndpoint,
            new NullLogger<GlobalExceptionHandlerMiddleware>(),
            redactor);

        var requestLoggingMiddleware = new RequestLoggingMiddleware(
            exceptionMiddleware.InvokeAsync,
            new NullLogger<RequestLoggingMiddleware>(),
            redactor);

        // Act
        await requestLoggingMiddleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        context.Response.ContentType.Should().Contain("application/json");

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var responseContent = await reader.ReadToEndAsync();

        // Must not expose database details
        responseContent.Should().NotContain("SuperSecret123!");
        responseContent.Should().NotContain("prod-sql");
        responseContent.Should().NotContain("TimeoutException");

        // Must be valid Result Pattern JSON
        var jsonDoc = JsonDocument.Parse(responseContent);
        var root = jsonDoc.RootElement;
        root.GetProperty("code").GetString().Should().Be("Server.UnexpectedError");
        root.GetProperty("message").GetString().Should().Contain("e2e_trace_id_456");
        root.GetProperty("type").GetInt32().Should().Be(0);
    }

    [Fact]
    public void PiiRedactor_ShouldSanitizeAllPiiVarieties()
    {
        // Arrange
        var redactor = new PiiRedactor();

        // Act & Assert Headers
        redactor.RedactHeader("Authorization", "Bearer eyJhbGciOiJIUzI1NiJ9.xxx.yyy").Should().Be("Bearer [REDACTED]");
        redactor.RedactHeader("Cookie", "session=123").Should().Be("[REDACTED]");
        redactor.RedactHeader("X-Api-Key", "my_secret_key").Should().Be("[REDACTED]");
        redactor.RedactHeader("User-Agent", "Mozilla/5.0").Should().Be("Mozilla/5.0");

        // Act & Assert Query
        redactor.RedactQueryString("?token=sec_tok&cpf=12345678901&page=2")
            .Should().Be("?token=[REDACTED]&cpf=[REDACTED]&page=2");

        // Act & Assert Exception Message
        var sanitizedMsg = redactor.SanitizeExceptionMessage("Failed: Server=10.0.0.1;Password=secretPass;User=admin;");
        sanitizedMsg.Should().Contain("Password=[REDACTED]");
        sanitizedMsg.Should().NotContain("secretPass");

        // Act & Assert Endpoints
        redactor.IsSensitiveEndpoint("/api/v1/auth/login").Should().BeTrue();
        redactor.IsSensitiveEndpoint("/api/v1/backoffice/auth/login").Should().BeTrue();
        redactor.IsSensitiveEndpoint("/api/v1/auth/change-password").Should().BeTrue();
        redactor.IsSensitiveEndpoint("/api/v1/breeding/plantel").Should().BeFalse();
    }
}
