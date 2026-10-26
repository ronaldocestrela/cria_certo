using System.Diagnostics;
using System.Text.Json;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.BuildingBlocks.Abstractions.Security;

namespace CriaCerto.Api.Middleware;

/// <summary>
/// Middleware de tratamento global de exceções não tratadas (LGPD Art. 46 / Result Pattern).
/// Intercepta falhas internas, registra detalhes de forma segura no log interno e retorna
/// payloads JSON padronizados com Error.Unexpected, sem vazar stack traces ou connection strings.
/// </summary>
public class GlobalExceptionHandlerMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;
    private readonly IPiiRedactor _redactor;

    public GlobalExceptionHandlerMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlerMiddleware> logger,
        IPiiRedactor redactor)
    {
        _next = next;
        _logger = logger;
        _redactor = redactor;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
            var sanitizedMessage = _redactor.SanitizeExceptionMessage(ex.Message);

            _logger.LogError(
                ex,
                "Exceção não tratada capturada pelo GlobalExceptionHandlerMiddleware. TraceId: {TraceId}, Rota: {Method} {Path}, Detalhes: {SanitizedDetails}",
                traceId,
                context.Request.Method,
                context.Request.Path,
                sanitizedMessage);

            if (context.Response.HasStarted)
            {
                _logger.LogWarning("A resposta HTTP já havia sido iniciada. Não foi possível enviar payload padronizado de erro para o TraceId: {TraceId}.", traceId);
                throw;
            }

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json; charset=utf-8";

            var error = Error.Unexpected(traceId);
            var payload = JsonSerializer.Serialize(error, JsonOptions);

            await context.Response.WriteAsync(payload);
        }
    }
}

public static class GlobalExceptionHandlerMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<GlobalExceptionHandlerMiddleware>();
    }
}
