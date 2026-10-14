using CriaCerto.BuildingBlocks.Abstractions.Results;

namespace CriaCerto.BuildingBlocks.Abstractions.Email;

public static class EmailErrors
{
    public static Error ApiKeyMissing =>
        Error.Failure("Email.ApiKeyMissing", "A chave de API do provedor de e-mail (Resend) não foi configurada.");

    public static Error InvalidRecipient(string? details = null) =>
        Error.Validation("Email.InvalidRecipient", details ?? "Destinatário de e-mail inválido.");

    public static Error RateLimited =>
        Error.Failure("Email.RateLimited", "Limite de requisições do provedor de e-mail excedido (Rate Limit). Tente novamente mais tarde.");

    public static Error ProviderError(string message) =>
        Error.Failure("Email.ProviderError", $"Erro retornado pelo provedor de e-mail: {message}");

    public static Error NetworkFailure(string message) =>
        Error.Failure("Email.NetworkFailure", $"Falha de conexão com o provedor de e-mail: {message}");
}
