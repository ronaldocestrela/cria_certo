using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace CriaCerto.Modules.Backoffice.Application.Domain.Services;

public partial class PiiDataMasker : IPiiDataMasker
{
    private static readonly HashSet<string> SensitiveKeyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "passwordhash", "secret", "secretkey", "mfasecretkey", "token",
        "recoverycodes", "cpf", "cnpj", "document", "email", "phone", "phonenumber"
    };

    public string MaskCpf(string? cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf))
            return string.Empty;

        var digits = ExtractDigits(cpf);
        if (digits.Length != 11)
        {
            if (digits.Length <= 3) return new string('*', digits.Length);
            return string.Concat(new string('*', digits.Length - 3), digits[^3..]);
        }

        // Formato: ***.456.789-**
        return $"***.{digits.Substring(3, 3)}.{digits.Substring(6, 3)}-**";
    }

    public string MaskCnpj(string? cnpj)
    {
        if (string.IsNullOrWhiteSpace(cnpj))
            return string.Empty;

        var digits = ExtractDigits(cnpj);
        if (digits.Length != 14)
        {
            if (digits.Length <= 4) return new string('*', digits.Length);
            return string.Concat(digits[..2], new string('*', digits.Length - 4), digits[^2..]);
        }

        // Formato: 12.***.***/0001-**
        return $"{digits.Substring(0, 2)}.***.***/{digits.Substring(8, 4)}-**";
    }

    public string MaskDocument(string? document)
    {
        if (string.IsNullOrWhiteSpace(document))
            return string.Empty;

        var digits = ExtractDigits(document);
        if (digits.Length == 11)
            return MaskCpf(document);

        if (digits.Length == 14)
            return MaskCnpj(document);

        if (digits.Length > 6)
            return string.Concat(digits[..2], new string('*', digits.Length - 4), digits[^2..]);

        return new string('*', digits.Length);
    }

    public string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            return email ?? string.Empty;

        var parts = email.Trim().Split('@');
        var local = parts[0];
        var domain = parts[1];

        if (local.Length <= 1)
            return $"{local}***@{domain}";

        if (local.Length == 2)
            return $"{local[0]}***{local[1]}@{domain}";

        return $"{local[0]}***{local[^1]}@{domain}";
    }

    public string MaskPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return string.Empty;

        var digits = ExtractDigits(phone);
        if (digits.Length == 11)
        {
            // Formato celular: (11) 9****-**21
            var ddd = digits.Substring(0, 2);
            var prefix = digits.Substring(2, 1);
            var lastTwo = digits.Substring(9, 2);
            return $"({ddd}) {prefix}****-**{lastTwo}";
        }

        if (digits.Length == 10)
        {
            // Formato fixo: (67) 3****-**66
            var ddd = digits.Substring(0, 2);
            var prefix = digits.Substring(2, 1);
            var lastTwo = digits.Substring(8, 2);
            return $"({ddd}) {prefix}****-**{lastTwo}";
        }

        if (digits.Length > 4)
            return string.Concat(new string('*', digits.Length - 4), digits[^4..]);

        return new string('*', digits.Length);
    }

    public string MaskIpAddress(string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
            return string.Empty;

        var ip = ipAddress.Trim();
        if (ip == "::1" || ip == "127.0.0.1")
            return ip;

        if (ip.Contains('.'))
        {
            var octets = ip.Split('.');
            if (octets.Length == 4)
                return $"{octets[0]}.{octets[1]}.***.***";
        }

        if (ip.Contains(':'))
        {
            var segments = ip.Split(':');
            if (segments.Length >= 2)
                return $"{segments[0]}:{segments[1]}:***";
        }

        return "***";
    }

    public string MaskPersonName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var tokens = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length <= 1)
            return name.Trim();

        var sb = new StringBuilder();
        sb.Append(tokens[0]);

        for (var i = 1; i < tokens.Length; i++)
        {
            var token = tokens[i];
            sb.Append(' ');
            if (token.Length > 0)
            {
                sb.Append(token[0]);
                sb.Append('.');
            }
        }

        return sb.ToString();
    }

    public string SanitizeJsonDetails(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return string.Empty;

        try
        {
            var node = JsonNode.Parse(json);
            if (node is null)
                return json;

            SanitizeNode(node);
            return node.ToJsonString();
        }
        catch
        {
            return json;
        }
    }

    private void SanitizeNode(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            var propertyNames = obj.Select(p => p.Key).ToList();
            foreach (var prop in propertyNames)
            {
                var val = obj[prop];
                if (val is null) continue;

                if (val is JsonObject || val is JsonArray)
                {
                    SanitizeNode(val);
                    continue;
                }

                if (SensitiveKeyNames.Contains(prop))
                {
                    var stringVal = val.ToString();
                    if (prop.Contains("password", StringComparison.OrdinalIgnoreCase) ||
                        prop.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
                        prop.Contains("token", StringComparison.OrdinalIgnoreCase))
                    {
                        obj[prop] = "***";
                    }
                    else if (prop.Contains("cpf", StringComparison.OrdinalIgnoreCase) ||
                             prop.Contains("cnpj", StringComparison.OrdinalIgnoreCase) ||
                             prop.Contains("document", StringComparison.OrdinalIgnoreCase))
                    {
                        obj[prop] = MaskDocument(stringVal);
                    }
                    else if (prop.Contains("email", StringComparison.OrdinalIgnoreCase))
                    {
                        obj[prop] = MaskEmail(stringVal);
                    }
                    else if (prop.Contains("phone", StringComparison.OrdinalIgnoreCase))
                    {
                        obj[prop] = MaskPhone(stringVal);
                    }
                    else
                    {
                        obj[prop] = "***";
                    }
                }
            }
        }
        else if (node is JsonArray arr)
        {
            foreach (var item in arr)
            {
                if (item is not null)
                    SanitizeNode(item);
            }
        }
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
}
