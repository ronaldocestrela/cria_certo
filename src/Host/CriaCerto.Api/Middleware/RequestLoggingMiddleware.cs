using System.Diagnostics;
using System.IO;
using System.Text;
using CriaCerto.BuildingBlocks.Abstractions.Security;

namespace CriaCerto.Api.Middleware;

/// <summary>
/// Middleware de auditoria e logging estruturado de requisições HTTP com redaction de PII e segredos (LGPD Art. 46).
/// Garante que cabeçalhos sensíveis (Authorization Bearer, Cookies), query strings e corpos de login/pagamento
/// nunca sejam gravados em texto claro nos coletores de logs.
/// </summary>
public class RequestLoggingMiddleware
{
    private const int MaxBodyCaptureLength = 16 * 1024; // 16 KB
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;
    private readonly IPiiRedactor _redactor;

    public RequestLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestLoggingMiddleware> logger,
        IPiiRedactor redactor)
    {
        _next = next;
        _logger = logger;
        _redactor = redactor;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Ignora rotas de health check para não poluir logs estruturados
        if (path.Equals("/health", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

        var sanitizedQuery = _redactor.RedactQueryString(context.Request.QueryString.Value ?? string.Empty);
        var isSensitiveEndpoint = _redactor.IsSensitiveEndpoint(path);

        string requestBodyPreview = "[EMPTY]";

        if (isSensitiveEndpoint)
        {
            requestBodyPreview = "[REDACTED_SENSITIVE_ENDPOINT]";
        }
        else if (context.Request.ContentLength is > 0 &&
                 context.Request.ContentType is not null &&
                 context.Request.ContentType.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            context.Request.EnableBuffering();

            using var reader = new StreamReader(
                context.Request.Body,
                Encoding.UTF8,
                leaveOpen: true);

            var rawBody = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;

            if (rawBody.Length > MaxBodyCaptureLength)
            {
                rawBody = rawBody[..MaxBodyCaptureLength];
            }

            requestBodyPreview = _redactor.RedactJson(rawBody);
        }

        // Executa o restante do pipeline
        await _next(context);

        stopwatch.Stop();
        var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;
        var statusCode = context.Response.StatusCode;

        if (statusCode >= 500)
        {
            _logger.LogError(
                "HTTP {Method} {Path}{Query} finalizou com {StatusCode} em {DurationMs:0.00}ms [TraceId: {TraceId}]. Body: {RequestBody}",
                context.Request.Method,
                path,
                sanitizedQuery,
                statusCode,
                elapsedMs,
                traceId,
                requestBodyPreview);
        }
        else if (statusCode >= 400)
        {
            _logger.LogWarning(
                "HTTP {Method} {Path}{Query} finalizou com {StatusCode} em {DurationMs:0.00}ms [TraceId: {TraceId}]. Body: {RequestBody}",
                context.Request.Method,
                path,
                sanitizedQuery,
                statusCode,
                elapsedMs,
                traceId,
                requestBodyPreview);
        }
        else
        {
            _logger.LogInformation(
                "HTTP {Method} {Path}{Query} finalizou com {StatusCode} em {DurationMs:0.00}ms [TraceId: {TraceId}]. Body: {RequestBody}",
                context.Request.Method,
                path,
                sanitizedQuery,
                statusCode,
                elapsedMs,
                traceId,
                requestBodyPreview);
        }
    }
}

public static class RequestLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestLoggingMiddleware>();
    }
}
