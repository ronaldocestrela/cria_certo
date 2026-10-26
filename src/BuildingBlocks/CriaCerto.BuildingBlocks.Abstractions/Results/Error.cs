namespace CriaCerto.BuildingBlocks.Abstractions.Results;

public sealed record Error(string Code, string Message, ErrorType Type)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);

    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);

    public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);

    public static Error Unexpected(string code, string message) => new(code, message, ErrorType.Failure);

    public static Error Unexpected(string? traceId = null) =>
        new("Server.UnexpectedError",
            string.IsNullOrWhiteSpace(traceId)
                ? "Ocorreu um erro interno inesperado. Por favor, tente novamente mais tarde."
                : $"Ocorreu um erro interno inesperado. Identificador de rastreamento: {traceId}.",
            ErrorType.Failure);
}