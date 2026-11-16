using CriaCerto.Api.Endpoints.Common;
using CriaCerto.Modules.Nutrition.Application.Contracts;
using CriaCerto.Modules.Nutrition.Application.Features.AnalyticsFeatures;
using CriaCerto.Modules.Nutrition.Application.Features.FeedingFeatures;
using CriaCerto.Modules.Nutrition.Application.Features.RationFeatures;
using CriaCerto.Modules.Nutrition.Application.Features.SiloStockFeatures;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CriaCerto.Api.Endpoints;

public static class NutritionEndpoints
{
    public static IEndpointRouteBuilder MapNutritionEndpoints(this IEndpointRouteBuilder app)
    {
        var nutrition = app.MapGroup("/api/nutrition")
            .RequireAuthorization()
            .WithTags("Nutrition");

        nutrition.MapGet("/silos", async (Guid tenantId, ISender sender) =>
        {
            var result = await sender.Send(new GetSiloStocksQuery(tenantId));
            return EndpointResultExtensions.ToHttpResult(result);
        });

        nutrition.MapPost("/silos", async (CreateSiloStockCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result, StatusCodes.Status201Created);
        });

        nutrition.MapPost("/silos/restock", async (RestockSiloCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result);
        });

        nutrition.MapGet("/rations", async (Guid tenantId, ISender sender) =>
        {
            var result = await sender.Send(new GetFeedRationsQuery(tenantId));
            return EndpointResultExtensions.ToHttpResult(result);
        });

        nutrition.MapPost("/rations", async (CreateFeedRationCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result, StatusCodes.Status201Created);
        });

        nutrition.MapPost("/supplementation", async (RecordSupplementationCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result, StatusCodes.Status201Created);
        });

        nutrition.MapPost("/tmr-batches", async (RecordFeedlotTmrCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result, StatusCodes.Status201Created);
        });

        nutrition.MapGet("/analytics/feed-conversion", async (Guid tenantId, Guid lotId, decimal totalWeightGainKg, ISender sender) =>
        {
            var result = await sender.Send(new GetFeedlotPerformanceQuery(tenantId, lotId, totalWeightGainKg));
            return EndpointResultExtensions.ToHttpResult(result);
        });

        nutrition.MapGet("/analytics/cost-per-arroba", async (Guid tenantId, Guid lotId, decimal totalWeightGainKg, decimal? carcassYieldPercentage, ISender sender) =>
        {
            var result = await sender.Send(new GetCostPerArrobaQuery(tenantId, lotId, totalWeightGainKg, carcassYieldPercentage));
            return EndpointResultExtensions.ToHttpResult(result);
        });

        return app;
    }
}

