using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Abstractions;

namespace CriaCerto.Modules.Tenancy.Application.Services;

public class SubscriptionUrlValidator : ISubscriptionUrlValidator
{
    private readonly HashSet<string> _allowedOrigins;
    private readonly string _defaultOrigin;

    private static readonly string[] DefaultTrustedOrigins =
    [
        "https://criacerto.com.br",
        "https://app.criacerto.com.br",
        "https://www.criacerto.com.br",
        "http://localhost:8081",
        "http://localhost:8080",
        "http://localhost:5000",
        "http://localhost:5001",
        "https://localhost:7001",
        "http://localhost:5173",
        "http://localhost:5205",
        "https://localhost:7269"
    ];

    public SubscriptionUrlValidator(IEnumerable<string>? allowedOrigins = null, string? defaultOrigin = null)
    {
        _allowedOrigins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Sempre incluir as origens canônicas oficiais da plataforma e portas locais padrão
        foreach (var trusted in DefaultTrustedOrigins)
        {
            _allowedOrigins.Add(trusted);
        }

        // Adicionar origens complementares fornecidas pela configuração
        if (allowedOrigins != null)
        {
            foreach (var origin in allowedOrigins)
            {
                if (!string.IsNullOrWhiteSpace(origin) && Uri.TryCreate(origin.Trim(), UriKind.Absolute, out var uri))
                {
                    _allowedOrigins.Add(uri.GetLeftPart(UriPartial.Authority).TrimEnd('/'));
                }
            }
        }

        _defaultOrigin = !string.IsNullOrWhiteSpace(defaultOrigin)
            ? defaultOrigin.TrimEnd('/')
            : (_allowedOrigins.Contains("https://criacerto.com.br") ? "https://criacerto.com.br" : _allowedOrigins.First());
    }

    public string DefaultOrigin => _defaultOrigin;

    public bool IsAllowedUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return true;
        }

        var trimmedUrl = url.Trim();

        // Bloquear bypass com protocolo relativo (//attacker.com) e backslashes
        if (trimmedUrl.StartsWith("//", StringComparison.Ordinal) ||
            trimmedUrl.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return false;
        }

        // Caminhos relativos válidos (ex.: /settings/subscription?success=true)
        if (trimmedUrl.StartsWith('/') && !trimmedUrl.StartsWith("//"))
        {
            return Uri.TryCreate(trimmedUrl, UriKind.Relative, out _);
        }

        // URLs absolutas
        if (Uri.TryCreate(trimmedUrl, UriKind.Absolute, out var uri))
        {
            // Apenas esquemas seguros http ou https
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            {
                return false;
            }

            // Não permitir UserInfo (ex.: https://victim.com@attacker.com)
            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                return false;
            }

            var authority = uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
            if (_allowedOrigins.Contains(authority))
            {
                return true;
            }

            // Permitir subdomínios oficiais seguros sob criacerto.com.br em HTTPS na porta padrão
            if (uri.Scheme == Uri.UriSchemeHttps &&
                uri.IsDefaultPort &&
                (string.Equals(uri.Host, "criacerto.com.br", StringComparison.OrdinalIgnoreCase) ||
                 uri.Host.EndsWith(".criacerto.com.br", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            return false;
        }

        return false;
    }

    public Result<string> ResolveSafeUrl(string? requestedUrl, string fallbackUrl)
    {
        if (string.IsNullOrWhiteSpace(requestedUrl))
        {
            return Result.Success(fallbackUrl);
        }

        var trimmedUrl = requestedUrl.Trim();

        if (!IsAllowedUrl(trimmedUrl))
        {
            return Result.Failure<string>(
                Error.Validation("Subscription.InvalidRedirectUrl", $"A URL de redirecionamento '{requestedUrl}' não é autorizada ou é inválida."));
        }

        // Se for relativo, resolve com a autoridade confiável
        if (trimmedUrl.StartsWith('/') && !trimmedUrl.StartsWith("//"))
        {
            string baseAuthority = _defaultOrigin;
            if (Uri.TryCreate(fallbackUrl, UriKind.Absolute, out var fallbackUri))
            {
                var fallbackAuthority = fallbackUri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
                if (_allowedOrigins.Contains(fallbackAuthority) || IsAllowedUrl(fallbackAuthority))
                {
                    baseAuthority = fallbackAuthority;
                }
            }

            return Result.Success($"{baseAuthority}{trimmedUrl}");
        }

        return Result.Success(trimmedUrl);
    }
}
