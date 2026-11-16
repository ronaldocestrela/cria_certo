using CriaCerto.Api.Endpoints.Common;
using CriaCerto.Modules.Breeding.Application.Domain;
using CriaCerto.Modules.Breeding.Application.Features.BreedingOps;
using CriaCerto.Modules.Breeding.Application.Features.Plantel;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CriaCerto.Api.Endpoints;

public static class BreedingEndpoints
{
    public static IEndpointRouteBuilder MapBreedingEndpoints(this IEndpointRouteBuilder app)
    {
        var breeding = app.MapGroup("/api/breeding")
            .RequireAuthorization()
            .WithTags("Breeding");

        breeding.MapGet("/cows", async (
            ISender sender,
            string? search,
            ReproductiveStatus? status,
            int page = 1,
            int pageSize = 25) =>
        {
            var result = await sender.Send(new ListCowsQuery(search, status, page, pageSize));
            return EndpointResultExtensions.ToHttpResult(result);
        });

        breeding.MapPost("/cows", async (CreateCowCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result, StatusCodes.Status201Created);
        });

        breeding.MapGet("/cows/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetCowQuery(id));
            return EndpointResultExtensions.ToHttpResult(result);
        });

        breeding.MapPut("/cows/{id:guid}", async (Guid id, UpdateCowCommand command, ISender sender) =>
        {
            if (id != command.Id)
                return Results.BadRequest(new { Error = "ID da URL diverge do ID do corpo da requisição." });

            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result);
        });

        breeding.MapGet("/bulls", async (Guid tenantId, ISender sender) =>
        {
            var result = await sender.Send(new ListBullsQuery(tenantId));
            return EndpointResultExtensions.ToHttpResult(result);
        });

        breeding.MapGet("/iatf-protocols", async (Guid tenantId, ISender sender) =>
        {
            var result = await sender.Send(new GetIatfProtocolsQuery(tenantId));
            return EndpointResultExtensions.ToHttpResult(result);
        });

        breeding.MapPost("/iatf-protocols", async (RegisterIatfProtocolCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result, StatusCodes.Status201Created);
        });

        breeding.MapPost("/diagnoses", async (RegisterPregnancyDiagnosisCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result, StatusCodes.Status201Created);
        });

        return app;
    }
}

