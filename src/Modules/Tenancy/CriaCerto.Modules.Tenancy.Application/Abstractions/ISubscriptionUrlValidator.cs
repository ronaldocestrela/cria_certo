using CriaCerto.BuildingBlocks.Abstractions.Results;

namespace CriaCerto.Modules.Tenancy.Application.Abstractions;

public interface ISubscriptionUrlValidator
{
    /// <summary>
    /// Verifica se a URL informada é segura e pertence às origens confiáveis ou é um caminho relativo válido.
    /// </summary>
    bool IsAllowedUrl(string? url);

    /// <summary>
    /// Valida e resolve uma URL segura. Se a URL solicitada for nula ou vazia, retorna o fallbackUrl.
    /// Retorna Result.Failure caso a URL seja inválida ou pertença a um domínio não autorizado (Open Redirect).
    /// </summary>
    Result<string> ResolveSafeUrl(string? requestedUrl, string fallbackUrl);
}
