using CriaCerto.Api.Endpoints.Common;
using CriaCerto.BuildingBlocks.Application.Features.GetReferenceBreeds;
using CriaCerto.Modules.Sanitary.Application.Features.GetVaccineCalendar;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CriaCerto.Api.Endpoints;

public static class ReferenceEndpoints
{
    public static IEndpointRouteBuilder MapReferenceEndpoints(this IEndpointRouteBuilder app)
    {
        var reference = app.MapGroup("/api/reference");

        reference.MapGet("/breeds", async (ISender sender) =>
        {
            var result = await sender.Send(new GetReferenceBreedsQuery());
            return EndpointResultExtensions.ToHttpResult(result);
        });

        reference.MapGet("/vaccines", async (ISender sender) =>
        {
            var result = await sender.Send(new GetVaccineCalendarQuery());
            return EndpointResultExtensions.ToHttpResult(result);
        });

        return app;
    }
}

