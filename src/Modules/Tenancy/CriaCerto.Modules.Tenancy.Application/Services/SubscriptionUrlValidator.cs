using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Abstractions;

namespace CriaCerto.Modules.Tenancy.Application.Services;

public class SubscriptionUrlValidator : ISubscriptionUrlValidator
{
    private readonly HashSet<string> _allowedOrigins;
    private readonly string _defaultOrigin;

    public SubscriptionUrlValidator(IEnumerable<string>? allowedOrigins = null, string? defaultOrigin = null)
    {
        _allowedOrigins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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

        if (_allowedOrigins.Count == 0)
        {
            _allowedOrigins.Add("http://localhost:8081");
            _allowedOrigins.Add("http://localhost:8080");
            _allowedOrigins.Add("http://localhost:5000");
            _allowedOrigins.Add("http://localhost:5001");
            _allowedOrigins.Add("https://localhost:7001");
            _allowedOrigins.Add("http://localhost:5173");
            _allowedOrigins.Add("http://localhost:5205");
            _allowedOrigins.Add("https://localhost:7269");
            _allowedOrigins.Add("https://criacerto.com.br");
            _allowedOrigins.Add("https://app.criacerto.com.br");
        }

        _defaultOrigin = !string.IsNullOrWhiteSpace(defaultOrigin)
            ? defaultOrigin.TrimEnd('/')
            : _allowedOrigins.First();
    }

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
            return _allowedOrigins.Contains(authority);
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
            if (Uri.TryCreate(fallbackUrl, UriKind.Absolute, out var fallbackUri) &&
                _allowedOrigins.Contains(fallbackUri.GetLeftPart(UriPartial.Authority).TrimEnd('/')))
            {
                baseAuthority = fallbackUri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
            }

            return Result.Success($"{baseAuthority}{trimmedUrl}");
        }

        return Result.Success(trimmedUrl);
    }
}
