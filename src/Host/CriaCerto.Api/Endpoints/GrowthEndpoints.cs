using CriaCerto.Api.Endpoints.Common;
using CriaCerto.Modules.Growth.Application.Contracts;
using CriaCerto.Modules.Growth.Application.Features.DispatchFeatures;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CriaCerto.Api.Endpoints;

public static class GrowthEndpoints
{
    public static IEndpointRouteBuilder MapGrowthEndpoints(this IEndpointRouteBuilder app)
    {
        var growth = app.MapGroup("/api/growth")
            .RequireAuthorization()
            .WithTags("Growth");

        growth.MapGet("/paddocks", async (Guid tenantId, ISender sender) =>
        {
            var result = await sender.Send(new GetPaddocksWithStockingRateQuery(tenantId));
            return EndpointResultExtensions.ToHttpResult(result);
        });

        growth.MapPost("/paddocks", async (CreatePaddockCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result, StatusCodes.Status201Created);
        });

        growth.MapGet("/lots", async (Guid tenantId, ISender sender) =>
        {
            var result = await sender.Send(new GetLotsQuery(tenantId));
            return EndpointResultExtensions.ToHttpResult(result);
        });

        growth.MapPost("/lots", async (CreateLotCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result, StatusCodes.Status201Created);
        });

        growth.MapPost("/lots/move", async (MoveLotToPaddockCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result, StatusCodes.Status200OK);
        });

        growth.MapPost("/lots/{id:guid}/close", async (Guid id, Guid tenantId, ISender sender) =>
        {
            var result = await sender.Send(new CloseLotCommand(id, tenantId));
            return EndpointResultExtensions.ToHttpResult(result, StatusCodes.Status200OK);
        });

        growth.MapPost("/dispatch/animal", async (DispatchAnimalCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result, StatusCodes.Status200OK);
        });

        growth.MapPost("/dispatch/lot", async (DispatchLotCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result, StatusCodes.Status200OK);
        });

        growth.MapPost("/weighings", async (RecordWeighingCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result, StatusCodes.Status201Created);
        });

        growth.MapPost("/weighings/batch", async (BatchRecordWeighingCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result, StatusCodes.Status201Created);
        });

        growth.MapGet("/weighings/history/{animalTagId}", async (Guid tenantId, string animalTagId, ISender sender) =>
        {
            var result = await sender.Send(new GetAnimalWeighingHistoryQuery(tenantId, animalTagId));
            return EndpointResultExtensions.ToHttpResult(result);
        });

        growth.MapGet("/weighings/lot-summary/{lotId:guid}", async (Guid tenantId, Guid lotId, ISender sender) =>
        {
            var result = await sender.Send(new GetLotWeighingSummaryQuery(tenantId, lotId));
            return EndpointResultExtensions.ToHttpResult(result);
        });

        growth.MapGet("/weighings/recent", async (Guid tenantId, Guid? lotId, int? top, ISender sender) =>
        {
            var result = await sender.Send(new GetRecentWeighingsQuery(tenantId, lotId, top ?? 50));
            return EndpointResultExtensions.ToHttpResult(result);
        });

        growth.MapPost("/weighings/import", async (IFormFile file, ScaleModelEnum scaleModel, Guid tenantId, Guid? lotId, DateTime? defaultWeighingDate, decimal? defaultCarcassYield, ISender sender) =>
        {
            if (file is null || file.Length == 0)
                return Results.BadRequest("Arquivo de balança não fornecido ou vazio.");

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);

            var command = new ImportWeighingFileCommand(
                ms.ToArray(),
                file.FileName,
                scaleModel,
                tenantId,
                lotId,
                defaultWeighingDate ?? DateTime.UtcNow,
                defaultCarcassYield ?? 50.0m);

            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result, StatusCodes.Status201Created);
        }).DisableAntiforgery();

        growth.MapGet("/weighings/anomalies", async (Guid tenantId, Guid? lotId, ISender sender) =>
        {
            var result = await sender.Send(new GetWeighingAnomaliesQuery(tenantId, lotId));
            return EndpointResultExtensions.ToHttpResult(result);
        });

        return app;
    }
}

