using System.Diagnostics;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using MediatR;

namespace CriaCerto.Modules.Backoffice.Application.Telemetry;

public sealed class BackofficeObservabilityBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : Result
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var isCommand = requestName.EndsWith("Command", StringComparison.OrdinalIgnoreCase);
        var operationType = isCommand ? "command" : "query";

        using var activity = BackofficeTelemetry.ActivitySource.StartActivity(
            $"Backoffice.{requestName}",
            ActivityKind.Internal);

        activity?.SetTag("backoffice.request_name", requestName);
        activity?.SetTag("backoffice.operation_type", operationType);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await next();

            stopwatch.Stop();
            var durationMs = stopwatch.Elapsed.TotalMilliseconds;

            BackofficeTelemetry.OperationDurationHistogram.Record(
                durationMs,
                new KeyValuePair<string, object?>("request_name", requestName),
                new KeyValuePair<string, object?>("operation_type", operationType),
                new KeyValuePair<string, object?>("is_success", response.IsSuccess));

            activity?.SetTag("backoffice.duration_ms", durationMs);
            activity?.SetTag("backoffice.is_success", response.IsSuccess);

            if (response.IsFailure)
            {
                activity?.SetTag("backoffice.error_code", response.Error.Code);
                activity?.SetStatus(ActivityStatusCode.Error, response.Error.Message);
            }
            else
            {
                activity?.SetStatus(ActivityStatusCode.Ok);
            }

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var durationMs = stopwatch.Elapsed.TotalMilliseconds;

            BackofficeTelemetry.OperationDurationHistogram.Record(
                durationMs,
                new KeyValuePair<string, object?>("request_name", requestName),
                new KeyValuePair<string, object?>("operation_type", operationType),
                new KeyValuePair<string, object?>("is_success", false),
                new KeyValuePair<string, object?>("exception", ex.GetType().Name));

            activity?.SetTag("backoffice.exception", ex.Message);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}
