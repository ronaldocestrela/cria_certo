using System.Security.Claims;
using System.Text;
using System.Text.Json;
using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.BuildingBlocks.Abstractions.Tenancy;
using CriaCerto.Modules.Breeding.Application.Domain;
using CriaCerto.Modules.Breeding.Application.Features.BreedingOps;
using CriaCerto.Modules.Growth.Application.Contracts;
using CriaCerto.Modules.Sanitary.Application.Contracts;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CriaCerto.Api.Endpoints;

public static class OfflineSyncEndpoints
{
    public static IEndpointRouteBuilder MapOfflineSyncEndpoints(this IEndpointRouteBuilder app)
    {
        var syncGroup = app.MapGroup("/api/v1/sync")
            .RequireAuthorization()
            .WithTags("OfflineSync");

        syncGroup.MapPost("/{moduleName}/{actionType}", HandleSyncOperationAsync);

        return app;
    }

    public static async Task<IResult> HandleSyncOperationAsync(
        string moduleName,
        string actionType,
        bool? force,
        HttpRequest request,
        ClaimsPrincipal userClaims,
        ITenantContext tenantContext,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var isForce = force ?? false;
        var resolvedTenantId = tenantContext.TenantId ?? Guid.Empty;
        if (resolvedTenantId == Guid.Empty)
        {
            var tenantClaim = userClaims.FindFirst("TenantId")?.Value;
            if (Guid.TryParse(tenantClaim, out var parsedTenantId))
            {
                resolvedTenantId = parsedTenantId;
            }
        }

        using var reader = new StreamReader(request.Body, Encoding.UTF8);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(rawBody))
        {
            return Results.BadRequest(new { Error = "Sync.EmptyPayload", Message = "O corpo da requisição de sincronização está vazio." });
        }

        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // Caso o cliente legado tenha serializado uma string JSON escapada, desescapa de forma resiliente
        if (rawBody.StartsWith('"') && rawBody.EndsWith('"'))
        {
            try
            {
                var unescaped = JsonSerializer.Deserialize<string>(rawBody, jsonOptions);
                if (!string.IsNullOrWhiteSpace(unescaped))
                {
                    rawBody = unescaped;
                }
            }
            catch
            {
                // Mantém rawBody original
            }
        }

        if (string.Equals(moduleName, "Breeding", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(actionType, "RegisterDiagnosis", StringComparison.OrdinalIgnoreCase))
            {
                using var doc = JsonDocument.Parse(rawBody);
                var root = doc.RootElement;

                var cowId = Guid.Empty;
                if (root.TryGetProperty("cowId", out var cowIdProp) || root.TryGetProperty("CowId", out cowIdProp) ||
                    root.TryGetProperty("sowId", out cowIdProp) || root.TryGetProperty("SowId", out cowIdProp))
                {
                    Guid.TryParse(cowIdProp.GetString(), out cowId);
                }

                var diagDate = DateTime.UtcNow;
                if ((root.TryGetProperty("diagnosisDate", out var dateProp) || root.TryGetProperty("DiagnosisDate", out dateProp)) &&
                    dateProp.ValueKind == JsonValueKind.String &&
                    DateTime.TryParse(dateProp.GetString(), out var parsedDate))
                {
                    diagDate = DateTime.SpecifyKind(parsedDate, DateTimeKind.Utc);
                }

                var isPregnant = false;
                if (root.TryGetProperty("isPregnant", out var pregProp) || root.TryGetProperty("IsPregnant", out pregProp))
                {
                    isPregnant = pregProp.ValueKind == JsonValueKind.True;
                }
                else if (root.TryGetProperty("result", out var resProp) || root.TryGetProperty("Result", out resProp))
                {
                    isPregnant = resProp.ValueKind == JsonValueKind.Number
                        ? resProp.GetInt32() == 1
                        : string.Equals(resProp.GetString(), "Pregnant", StringComparison.OrdinalIgnoreCase);
                }

                string? notes = null;
                if ((root.TryGetProperty("notes", out var notesProp) || root.TryGetProperty("Notes", out notesProp)) &&
                    notesProp.ValueKind == JsonValueKind.String)
                {
                    notes = notesProp.GetString();
                }

                var command = new RegisterPregnancyDiagnosisCommand(
                    cowId,
                    diagDate,
                    DiagnosisMethod.Ultrasound,
                    isPregnant,
                    resolvedTenantId,
                    null,
                    notes,
                    isForce);

                var result = await sender.Send(command, cancellationToken);
                if (result.IsFailure && result.Error.Type == ErrorType.Conflict && !isForce)
                {
                    return Results.Conflict(new { Error = result.Error.Code, Message = result.Error.Message });
                }

                return ToHttpResult(result);
            }

            if (string.Equals(actionType, "RecordInsemination", StringComparison.OrdinalIgnoreCase))
            {
                using var doc = JsonDocument.Parse(rawBody);
                var root = doc.RootElement;

                var cowTag = string.Empty;
                if (root.TryGetProperty("cowTag", out var tagProp) || root.TryGetProperty("CowTag", out tagProp) ||
                    root.TryGetProperty("tagId", out tagProp) || root.TryGetProperty("TagId", out tagProp))
                {
                    cowTag = tagProp.GetString() ?? string.Empty;
                }

                var semenCode = "LOTE-CAMPO";
                if ((root.TryGetProperty("semenBatchCode", out var semenProp) || root.TryGetProperty("SemenBatchCode", out semenProp) ||
                     root.TryGetProperty("boarOrSemenRef", out semenProp) || root.TryGetProperty("BoarOrSemenRef", out semenProp)) &&
                    semenProp.ValueKind == JsonValueKind.String)
                {
                    semenCode = semenProp.GetString() ?? "LOTE-CAMPO";
                }

                var command = new RecordInseminationCommand(
                    cowTag,
                    DateTime.UtcNow,
                    semenCode,
                    resolvedTenantId,
                    null,
                    isForce);

                var result = await sender.Send(command, cancellationToken);
                if (result.IsFailure && result.Error.Type == ErrorType.Conflict && !isForce)
                {
                    return Results.Conflict(new { Error = result.Error.Code, Message = result.Error.Message });
                }

                return ToHttpResult(result);
            }

            if (string.Equals(actionType, "RegisterBreedingBatch", StringComparison.OrdinalIgnoreCase))
            {
                using var doc = JsonDocument.Parse(rawBody);
                var root = doc.RootElement;

                var eventDate = DateTime.UtcNow;
                if ((root.TryGetProperty("eventDate", out var dateProp) || root.TryGetProperty("EventDate", out dateProp)) &&
                    dateProp.ValueKind == JsonValueKind.String &&
                    DateTime.TryParse(dateProp.GetString(), out var parsedDate))
                {
                    eventDate = DateTime.SpecifyKind(parsedDate, DateTimeKind.Utc);
                }

                var syncLines = new List<BreedingBatchSyncLine>();
                if ((root.TryGetProperty("lines", out var linesProp) || root.TryGetProperty("Lines", out linesProp)) &&
                    linesProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in linesProp.EnumerateArray())
                    {
                        var tagId = string.Empty;
                        if (item.TryGetProperty("tagId", out var tProp) || item.TryGetProperty("TagId", out tProp))
                        {
                            tagId = tProp.GetString() ?? string.Empty;
                        }

                        string? semenRef = null;
                        if ((item.TryGetProperty("boarOrSemenRef", out var sProp) || item.TryGetProperty("BoarOrSemenRef", out sProp)) &&
                            sProp.ValueKind == JsonValueKind.String)
                        {
                            semenRef = sProp.GetString();
                        }

                        syncLines.Add(new BreedingBatchSyncLine(tagId, semenRef, null));
                    }
                }

                var command = new RegisterBreedingBatchCommand(eventDate, syncLines, resolvedTenantId, isForce);
                var result = await sender.Send(command, cancellationToken);
                if (result.IsFailure && result.Error.Type == ErrorType.Conflict && !isForce)
                {
                    return Results.Conflict(new { Error = result.Error.Code, Message = result.Error.Message });
                }

                return ToHttpResult(result);
            }
        }
        else if (string.Equals(moduleName, "Growth", StringComparison.OrdinalIgnoreCase) &&
                 string.Equals(actionType, "RecordWeighing", StringComparison.OrdinalIgnoreCase))
        {
            var parsedCommand = JsonSerializer.Deserialize<RecordWeighingCommand>(rawBody, jsonOptions);
            if (parsedCommand is null)
            {
                return Results.BadRequest(new { Error = "Sync.InvalidPayload", Message = "Payload de pesagem inválido." });
            }

            var command = parsedCommand.TenantId == Guid.Empty
                ? parsedCommand with { TenantId = resolvedTenantId }
                : parsedCommand;

            var result = await sender.Send(command, cancellationToken);
            if (result.IsFailure && result.Error.Type == ErrorType.Conflict && !isForce)
            {
                return Results.Conflict(new { Error = result.Error.Code, Message = result.Error.Message });
            }

            return ToHttpResult(result);
        }
        else if (string.Equals(moduleName, "Sanitary", StringComparison.OrdinalIgnoreCase) &&
                 (string.Equals(actionType, "ApplyVaccine", StringComparison.OrdinalIgnoreCase) ||
                  string.Equals(actionType, "ApplyTreatment", StringComparison.OrdinalIgnoreCase)))
        {
            var command = JsonSerializer.Deserialize<ApplyTreatmentCommand>(rawBody, jsonOptions);
            if (command is null)
            {
                return Results.BadRequest(new { Error = "Sync.InvalidPayload", Message = "Payload sanitário inválido." });
            }

            var result = await sender.Send(command, cancellationToken);
            if (result.IsFailure && result.Error.Type == ErrorType.Conflict && !isForce)
            {
                return Results.Conflict(new { Error = result.Error.Code, Message = result.Error.Message });
            }

            return ToHttpResult(result);
        }

        return Results.BadRequest(new
        {
            Error = "Sync.UnsupportedAction",
            Message = $"Ação '{moduleName}/{actionType}' não suportada."
        });
    }

    private static IResult ToHttpResult<TValue>(Result<TValue> result, int successStatusCode = StatusCodes.Status200OK)
    {
        if (result.IsSuccess)
        {
            return successStatusCode == StatusCodes.Status200OK
                ? Results.Ok(result.Value)
                : Results.Json(result.Value, statusCode: successStatusCode);
        }

        return Results.Json(result.Error, statusCode: ToStatusCode(result.Error.Type));
    }

    private static int ToStatusCode(ErrorType errorType) => errorType switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Unauthorized => StatusCodes.Status403Forbidden,
        _ => StatusCodes.Status400BadRequest
    };
}

