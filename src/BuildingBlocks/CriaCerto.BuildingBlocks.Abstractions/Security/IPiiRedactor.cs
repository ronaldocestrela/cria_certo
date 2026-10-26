namespace CriaCerto.BuildingBlocks.Abstractions.Security;

/// <summary>
/// Contrato para sanitização e redaction de dados pessoais (PII), segredos e credenciais
/// em logs HTTP, traces de telemetria e exceções (LGPD Art. 46).
/// </summary>
public interface IPiiRedactor
{
    /// <summary>
    /// Redige o valor de um cabeçalho HTTP caso ele seja sensível (ex: Authorization, Cookie, etc.).
    /// </summary>
    string RedactHeader(string headerName, string headerValue);

    /// <summary>
    /// Sanitiza parâmetros sensíveis contidos em uma query string de URL.
    /// </summary>
    string RedactQueryString(string queryString);

    /// <summary>
    /// Redige recursivamente propriedades sensíveis em um payload JSON formatado.
    /// </summary>
    string RedactJson(string? json);

    /// <summary>
    /// Sanitiza textos livres e mensagens de log substituindo potenciais tokens, senhas e números de documentos.
    /// </summary>
    string RedactText(string? text);

    /// <summary>
    /// Remove connection strings, senhas em texto puro e segredos de mensagens de exceção.
    /// </summary>
    string SanitizeExceptionMessage(string? message);

    /// <summary>
    /// Determina se a rota HTTP é considerada de alta sensibilidade (ex: login, redefinição de senha, etc.),
    /// cujo payload do corpo deve ser completamente suprimido em logs.
    /// </summary>
    bool IsSensitiveEndpoint(string? path);
}
