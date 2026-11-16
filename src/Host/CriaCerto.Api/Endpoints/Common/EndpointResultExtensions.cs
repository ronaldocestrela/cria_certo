using CriaCerto.BuildingBlocks.Abstractions.Results;
using Microsoft.AspNetCore.Http;

namespace CriaCerto.Api.Endpoints.Common;

public static class EndpointResultExtensions
{
    public static IResult ToCommandHttpResult(Result result, int successStatusCode = StatusCodes.Status200OK)
    {
        if (result.IsSuccess)
        {
            return successStatusCode == StatusCodes.Status200OK
                ? Results.Ok()
                : Results.StatusCode(successStatusCode);
        }

        return Results.Json(result.Error, statusCode: ToErrorStatusCode(result.Error));
    }

    public static IResult ToHttpResult<TValue>(Result<TValue> result, int successStatusCode = StatusCodes.Status200OK)
    {
        if (result.IsSuccess)
        {
            return successStatusCode == StatusCodes.Status200OK
                ? Results.Ok(result.Value)
                : Results.Json(result.Value, statusCode: successStatusCode);
        }

        return Results.Json(result.Error, statusCode: ToErrorStatusCode(result.Error));
    }

    public static int ToErrorStatusCode(Error error) => error.Code switch
    {
        "Server.UnexpectedError" => StatusCodes.Status500InternalServerError,
        _ => ToStatusCode(error.Type)
    };

    public static int ToStatusCode(ErrorType errorType) => errorType switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Unauthorized => StatusCodes.Status403Forbidden,
        _ => StatusCodes.Status400BadRequest
    };
}

