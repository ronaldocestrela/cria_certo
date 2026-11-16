using CriaCerto.Api.Endpoints.Common;
using CriaCerto.Modules.Calving.Application.Contracts;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CriaCerto.Api.Endpoints;

public static class CalvingEndpoints
{
    public static IEndpointRouteBuilder MapCalvingEndpoints(this IEndpointRouteBuilder app)
    {
        var calving = app.MapGroup("/api/calving")
            .RequireAuthorization()
            .WithTags("Calving");

        calving.MapGet("/records", async (Guid tenantId, ISender sender) =>
        {
            var result = await sender.Send(new GetCalvingRecordsQuery(tenantId));
            return EndpointResultExtensions.ToHttpResult(result);
        });

        calving.MapGet("", async (Guid tenantId, ISender sender) =>
        {
            var result = await sender.Send(new GetCalvingRecordsQuery(tenantId));
            return EndpointResultExtensions.ToHttpResult(result);
        });

        calving.MapPost("/calvings", async (RegisterCalvingCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result, StatusCodes.Status201Created);
        });

        calving.MapPost("/weanings", async (RegisterWeaningCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result, StatusCodes.Status201Created);
        });

        return app;
    }
}

