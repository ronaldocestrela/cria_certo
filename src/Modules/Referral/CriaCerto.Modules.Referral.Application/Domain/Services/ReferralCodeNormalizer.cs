using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CriaCerto.Modules.Referral.Application.Domain.Services;

public static partial class ReferralCodeNormalizer
{
    [GeneratedRegex(@"[^A-Z0-9\s-]")]
    private static partial Regex SpecialCharactersRegex();

    [GeneratedRegex(@"[\s-]+")]
    private static partial Regex ConsecutiveSeparatorsRegex();

    public static string NormalizeSlug(string? input, int maxLength = 50)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return GenerateFallbackCode();
        }

        // 1. Remove accents (decomposite unicode characters)
        var normalizedString = input.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder();

        foreach (var c in normalizedString)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }

        var withoutAccents = stringBuilder.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant();

        // 2. Remove non-alphanumeric except spaces and hyphens
        var cleanChars = SpecialCharactersRegex().Replace(withoutAccents, string.Empty);

        // 3. Replace spaces and consecutive hyphens with a single hyphen
        var slug = ConsecutiveSeparatorsRegex().Replace(cleanChars, "-").Trim('-');

        if (string.IsNullOrWhiteSpace(slug) || slug.Length < 3)
        {
            return GenerateFallbackCode();
        }

        if (slug.Length > maxLength)
        {
            slug = slug[..maxLength].TrimEnd('-');
        }

        return slug;
    }

    private static string GenerateFallbackCode()
    {
        var randomSuffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        return $"PARC-{randomSuffix}";
    }
}
