using CriaCerto.Api.Endpoints.Common;
using CriaCerto.Modules.Sanitary.Application.Contracts;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CriaCerto.Api.Endpoints;

public static class SanitaryEndpoints
{
    public static IEndpointRouteBuilder MapSanitaryEndpoints(this IEndpointRouteBuilder app)
    {
        var sanitary = app.MapGroup("/api/sanitary")
            .RequireAuthorization();

        sanitary.MapGet("/campaigns", async (ISender sender) =>
        {
            var result = await sender.Send(new GetActiveCampaignsQuery());
            return EndpointResultExtensions.ToHttpResult(result);
        });

        sanitary.MapPost("/campaigns", async (CreateVaccinationCampaignCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result, StatusCodes.Status201Created);
        });

        sanitary.MapPost("/treatments", async (ApplyTreatmentCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return EndpointResultExtensions.ToHttpResult(result, StatusCodes.Status201Created);
        });

        sanitary.MapGet("/treatments", async (ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetTreatmentsQuery(), cancellationToken);
            return EndpointResultExtensions.ToHttpResult(result);
        });

        sanitary.MapGet("/slaughter-validation/{animalId:guid}", async (Guid animalId, ISender sender) =>
        {
            var result = await sender.Send(new ValidateSlaughterEligibilityQuery(animalId));
            return EndpointResultExtensions.ToHttpResult(result);
        });

        return app;
    }
}

