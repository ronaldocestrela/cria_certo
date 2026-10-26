using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CriaCerto.BuildingBlocks.Abstractions.Security;

namespace CriaCerto.BuildingBlocks.Infrastructure.Security;

/// <summary>
/// Motor central de sanitização e redaction de PII, credenciais e segredos em logs e telemetria (LGPD Art. 46).
/// </summary>
public partial class PiiRedactor : IPiiRedactor
{
    private static readonly HashSet<string> SensitiveHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization", "Cookie", "Set-Cookie", "X-Api-Key", "Proxy-Authorization", "Api-Key"
    };

    private static readonly HashSet<string> SensitiveQueryParamNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "token", "password", "rawPassword", "secret", "secretKey", "key", "pixKey",
        "cpf", "cnpj", "code", "cvv", "securityCode", "refreshToken", "sessionToken"
    };

    private static readonly HashSet<string> SensitiveJsonKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "rawpassword", "newpassword", "currentpassword", "newrawpassword",
        "secret", "secretkey", "mfasecretkey", "token", "sessiontoken", "refreshtoken",
        "recoverycodes", "cvv", "securitycode", "cardnumber", "pixkey"
    };

    private static readonly string[] SensitiveEndpoints =
    [
        "/api/v1/auth/login",
        "/api/v1/backoffice/auth/login",
        "/api/v1/auth/change-password",
        "/api/v1/auth/reset-password",
        "/api/v1/auth/account/delete"
    ];

    public string RedactHeader(string headerName, string headerValue)
    {
        if (string.IsNullOrWhiteSpace(headerName) || string.IsNullOrWhiteSpace(headerValue))
            return headerValue;

        if (string.Equals(headerName, "Authorization", StringComparison.OrdinalIgnoreCase))
        {
            if (headerValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return "Bearer [REDACTED]";
            }

            return "[REDACTED]";
        }

        if (SensitiveHeaders.Contains(headerName))
        {
            return "[REDACTED]";
        }

        return headerValue;
    }

    public string RedactQueryString(string queryString)
    {
        if (string.IsNullOrWhiteSpace(queryString))
            return string.Empty;

        var hasLeadingQuestion = queryString.StartsWith('?');
        var rawQuery = hasLeadingQuestion ? queryString[1..] : queryString;

        var pairs = rawQuery.Split('&', StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();

        if (hasLeadingQuestion)
            sb.Append('?');

        for (var i = 0; i < pairs.Length; i++)
        {
            if (i > 0)
                sb.Append('&');

            var pair = pairs[i];
            var equalIdx = pair.IndexOf('=');

            if (equalIdx < 0)
            {
                sb.Append(pair);
                continue;
            }

            var key = pair[..equalIdx];
            var val = pair[(equalIdx + 1)..];

            if (SensitiveQueryParamNames.Contains(key))
            {
                sb.Append(key).Append("=[REDACTED]");
            }
            else
            {
                sb.Append(key).Append('=').Append(val);
            }
        }

        return sb.ToString();
    }

    private static readonly System.Text.Json.JsonSerializerOptions SerializerOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public string RedactJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return string.Empty;

        try
        {
            var node = JsonNode.Parse(json);
            if (node is null)
                return json;

            SanitizeJsonNode(node);
            return node.ToJsonString(SerializerOptions);
        }
        catch
        {
            return json;
        }
    }

    public string RedactText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var result = ConnectionStringPasswordRegex().Replace(text, "$1[REDACTED]");
        result = JwtBearerRegex().Replace(result, "Bearer [REDACTED]");
        return result;
    }

    public string SanitizeExceptionMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return string.Empty;

        var sanitized = ConnectionStringPasswordRegex().Replace(message, "$1[REDACTED]");
        sanitized = JwtBearerRegex().Replace(sanitized, "Bearer [REDACTED]");
        return sanitized;
    }

    public bool IsSensitiveEndpoint(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var cleanPath = path.TrimEnd('/');

        foreach (var sensitive in SensitiveEndpoints)
        {
            if (cleanPath.Equals(sensitive, StringComparison.OrdinalIgnoreCase) ||
                cleanPath.StartsWith(sensitive + "/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        if (cleanPath.Equals("/api/v1/backoffice/referral/payouts", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private void SanitizeJsonNode(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            var keys = obj.Select(k => k.Key).ToList();
            foreach (var key in keys)
            {
                var val = obj[key];
                if (val is null) continue;

                if (val is JsonObject || val is JsonArray)
                {
                    SanitizeJsonNode(val);
                    continue;
                }

                var keyLower = key.ToLowerInvariant();

                if (SensitiveJsonKeys.Contains(keyLower) ||
                    keyLower.Contains("password") ||
                    keyLower.Contains("secret") ||
                    keyLower.Contains("token"))
                {
                    obj[key] = "[REDACTED]";
                }
                else if (keyLower.Contains("cpf"))
                {
                    obj[key] = MaskCpf(val.ToString());
                }
                else if (keyLower.Contains("cnpj"))
                {
                    obj[key] = MaskCnpj(val.ToString());
                }
                else if (keyLower.Contains("document"))
                {
                    obj[key] = MaskDocument(val.ToString());
                }
                else if (keyLower.Contains("email"))
                {
                    obj[key] = MaskEmail(val.ToString());
                }
                else if (keyLower.Contains("phone"))
                {
                    obj[key] = MaskPhone(val.ToString());
                }
            }
        }
        else if (node is JsonArray arr)
        {
            foreach (var item in arr)
            {
                if (item is not null)
                    SanitizeJsonNode(item);
            }
        }
    }

    private static string MaskCpf(string? cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf))
            return string.Empty;

        var digits = ExtractDigits(cpf);
        if (digits.Length != 11)
            return digits.Length <= 3 ? new string('*', digits.Length) : string.Concat(new string('*', digits.Length - 3), digits[^3..]);

        return $"***.{digits.Substring(3, 3)}.{digits.Substring(6, 3)}-**";
    }

    private static string MaskCnpj(string? cnpj)
    {
        if (string.IsNullOrWhiteSpace(cnpj))
            return string.Empty;

        var digits = ExtractDigits(cnpj);
        if (digits.Length != 14)
            return digits.Length <= 4 ? new string('*', digits.Length) : string.Concat(digits[..2], new string('*', digits.Length - 4), digits[^2..]);

        return $"{digits.Substring(0, 2)}.***.***/{digits.Substring(8, 4)}-**";
    }

    private static string MaskDocument(string? document)
    {
        if (string.IsNullOrWhiteSpace(document))
            return string.Empty;

        var digits = ExtractDigits(document);
        if (digits.Length == 11) return MaskCpf(document);
        if (digits.Length == 14) return MaskCnpj(document);
        return digits.Length > 6 ? string.Concat(digits[..2], new string('*', digits.Length - 4), digits[^2..]) : new string('*', digits.Length);
    }

    private static string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            return email ?? string.Empty;

        var parts = email.Trim().Split('@');
        var local = parts[0];
        var domain = parts[1];

        if (local.Length <= 1)
            return $"{local}***@{domain}";

        return $"{local[0]}***{local[^1]}@{domain}";
    }

    private static string MaskPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return string.Empty;

        var digits = ExtractDigits(phone);
        if (digits.Length == 11)
            return $"({digits[..2]}) {digits.Substring(2, 1)}****-**{digits[^2..]}";

        if (digits.Length == 10)
            return $"({digits[..2]}) {digits.Substring(2, 1)}****-**{digits[^2..]}";

        return digits.Length > 4 ? string.Concat(new string('*', digits.Length - 4), digits[^4..]) : new string('*', digits.Length);
    }

    private static string ExtractDigits(string input)
    {
        var sb = new StringBuilder(input.Length);
        foreach (var ch in input)
        {
            if (char.IsAsciiDigit(ch))
                sb.Append(ch);
        }
        return sb.ToString();
    }

    [GeneratedRegex(@"(?i)((?:password|pwd)\s*=)[^;]+", RegexOptions.Compiled)]
    private static partial Regex ConnectionStringPasswordRegex();

    [GeneratedRegex(@"(?i)Bearer\s+[A-Za-z0-9\-_.]+\.[A-Za-z0-9\-_.]+\.[A-Za-z0-9\-_.]+", RegexOptions.Compiled)]
    private static partial Regex JwtBearerRegex();
}
