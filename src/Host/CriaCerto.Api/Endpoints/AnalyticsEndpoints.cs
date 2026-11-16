using CriaCerto.Api.Endpoints.Common;
using CriaCerto.Modules.Analytics.Application.Contracts;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CriaCerto.Api.Endpoints;

public static class AnalyticsEndpoints
{
    public static IEndpointRouteBuilder MapAnalyticsEndpoints(this IEndpointRouteBuilder app)
    {
        var analytics = app.MapGroup("/api/analytics")
            .RequireAuthorization();

        analytics.MapGet("/dashboard", async (ISender sender, Guid? tenantId) =>
        {
            var result = await sender.Send(new GetTenantExecutiveDashboardQuery(tenantId));
            return EndpointResultExtensions.ToHttpResult(result);
        });

        analytics.MapGet("/executive-scorecard", async (
            int? totalCows,
            int? pregnantCows,
            int? calvesWeaned,
            decimal? totalPastureHectares,
            decimal? totalAnimalUnits,
            decimal? averageGpdKg,
            decimal? averageCostPerArroba,
            int? animalsUnderWithdrawal,
            Guid? tenantId,
            ISender sender) =>
        {
            if (totalCows.HasValue && pregnantCows.HasValue && calvesWeaned.HasValue)
            {
                var query = new GetExecutiveAnalyticsQuery(
                    totalCows.Value,
                    pregnantCows.Value,
                    calvesWeaned.Value,
                    totalPastureHectares ?? 0m,
                    totalAnimalUnits ?? 0m,
                    averageGpdKg ?? 0m,
                    averageCostPerArroba ?? 0m,
                    animalsUnderWithdrawal ?? 0);
                var result = await sender.Send(query);
                return EndpointResultExtensions.ToHttpResult(result);
            }

            var dashboardResult = await sender.Send(new GetTenantExecutiveDashboardQuery(tenantId));
            if (dashboardResult.IsSuccess)
            {
                return Results.Ok(dashboardResult.Value.Scorecard);
            }
            return EndpointResultExtensions.ToHttpResult(dashboardResult);
        });

        analytics.MapPost("/export", async (ExportBovineReportQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            if (result.IsSuccess)
            {
                return Results.File(
                    fileContents: result.Value.FileContents,
                    contentType: result.Value.ContentType,
                    fileDownloadName: result.Value.FileName);
            }

            return EndpointResultExtensions.ToHttpResult(result);
        });

        analytics.MapPost("/export-csv", async (ExecutiveScorecardDto scorecard, ISender sender) =>
        {
            var result = await sender.Send(new ExportBovineReportQuery(scorecard));
            if (result.IsSuccess)
            {
                return Results.File(
                    fileContents: result.Value.FileContents,
                    contentType: result.Value.ContentType,
                    fileDownloadName: result.Value.FileName);
            }

            return EndpointResultExtensions.ToHttpResult(result);
        });

        return app;
    }
}

