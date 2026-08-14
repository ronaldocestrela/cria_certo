using CriaCerto.BuildingBlocks.Abstractions.Results;
using CriaCerto.Modules.Tenancy.Application.Domain.Errors;

namespace CriaCerto.Modules.Tenancy.Application.Domain;

public static class TenantSegmentationCatalog
{
    public static class SizeSegments
    {
        public const string Micro = "Micro";
        public const string Small = "Small";
        public const string Medium = "Medium";
        public const string Large = "Large";

        public static readonly IReadOnlyCollection<string> All = [Micro, Small, Medium, Large];
    }

    public static class CommercialRegions
    {
        public const string Norte = "Norte";
        public const string Nordeste = "Nordeste";
        public const string CentroOeste = "CentroOeste";
        public const string Sudeste = "Sudeste";
        public const string Sul = "Sul";

        public static readonly IReadOnlyCollection<string> All = [Norte, Nordeste, CentroOeste, Sudeste, Sul];
    }

    public static class ProductiveProfiles
    {
        public const string Corte = "Corte";
        public const string Leite = "Leite";
        public const string Misto = "Misto";
        public const string Cria = "Cria";
        public const string Recria = "Recria";
        public const string Engorda = "Engorda";
        public const string Confinamento = "Confinamento";

        public static readonly IReadOnlyCollection<string> All =
            [Corte, Leite, Misto, Cria, Recria, Engorda, Confinamento];
    }

    public static class ChurnRisks
    {
        public const string None = "None";
        public const string Low = "Low";
        public const string Medium = "Medium";
        public const string High = "High";
        public const string Critical = "Critical";

        public static readonly IReadOnlyCollection<string> All = [None, Low, Medium, High, Critical];
    }

    public static class TagCategories
    {
        public const string Support = "Support";
        public const string CustomerSuccess = "CustomerSuccess";
        public const string Retention = "Retention";

        public static readonly IReadOnlyCollection<string> All = [Support, CustomerSuccess, Retention];
    }

    private static readonly Dictionary<string, string> StateToRegion = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AC"] = CommercialRegions.Norte,
        ["AP"] = CommercialRegions.Norte,
        ["AM"] = CommercialRegions.Norte,
        ["PA"] = CommercialRegions.Norte,
        ["RO"] = CommercialRegions.Norte,
        ["RR"] = CommercialRegions.Norte,
        ["TO"] = CommercialRegions.Norte,
        ["AL"] = CommercialRegions.Nordeste,
        ["BA"] = CommercialRegions.Nordeste,
        ["CE"] = CommercialRegions.Nordeste,
        ["MA"] = CommercialRegions.Nordeste,
        ["PB"] = CommercialRegions.Nordeste,
        ["PE"] = CommercialRegions.Nordeste,
        ["PI"] = CommercialRegions.Nordeste,
        ["RN"] = CommercialRegions.Nordeste,
        ["SE"] = CommercialRegions.Nordeste,
        ["DF"] = CommercialRegions.CentroOeste,
        ["GO"] = CommercialRegions.CentroOeste,
        ["MT"] = CommercialRegions.CentroOeste,
        ["MS"] = CommercialRegions.CentroOeste,
        ["ES"] = CommercialRegions.Sudeste,
        ["MG"] = CommercialRegions.Sudeste,
        ["RJ"] = CommercialRegions.Sudeste,
        ["SP"] = CommercialRegions.Sudeste,
        ["PR"] = CommercialRegions.Sul,
        ["RS"] = CommercialRegions.Sul,
        ["SC"] = CommercialRegions.Sul
    };

    public const int MaxExportRows = 10_000;
    public const int MaxPageSize = 100;
    public const int DefaultPageSize = 20;

    public static string ResolveSizeSegmentFromCapacity(int capacity) =>
        capacity switch
        {
            < 100 => SizeSegments.Micro,
            < 500 => SizeSegments.Small,
            < 2500 => SizeSegments.Medium,
            _ => SizeSegments.Large
        };

    public static string ResolveCommercialRegionFromState(string state)
    {
        var normalized = state?.Trim().ToUpperInvariant() ?? string.Empty;
        return StateToRegion.TryGetValue(normalized, out var region)
            ? region
            : CommercialRegions.CentroOeste;
    }

    public static Result ValidateSizeSegment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !SizeSegments.All.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            return Result.Failure(TenancyErrors.InvalidSegmentation);
        }

        return Result.Success();
    }

    public static Result ValidateCommercialRegion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !CommercialRegions.All.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            return Result.Failure(TenancyErrors.InvalidSegmentation);
        }

        return Result.Success();
    }

    public static Result ValidateProductiveProfile(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !ProductiveProfiles.All.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            return Result.Failure(TenancyErrors.InvalidSegmentation);
        }

        return Result.Success();
    }

    public static Result ValidateChurnRisk(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !ChurnRisks.All.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            return Result.Failure(TenancyErrors.InvalidSegmentation);
        }

        return Result.Success();
    }

    public static Result ValidateTagCategory(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !TagCategories.All.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            return Result.Failure(TenancyErrors.InvalidTagCategory);
        }

        return Result.Success();
    }

    public static string NormalizeSegmentValue(string value, IReadOnlyCollection<string> allowedValues)
    {
        var match = allowedValues.FirstOrDefault(v => v.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase));
        return match ?? value.Trim();
    }

    public static string GenerateTagSlug(string name)
    {
        var normalized = name.Trim().ToLowerInvariant();
        var chars = normalized
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray();
        var slug = new string(chars);
        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        return slug.Trim('-');
    }

    public static int ClampPageSize(int pageSize) =>
        Math.Clamp(pageSize <= 0 ? DefaultPageSize : pageSize, 1, MaxPageSize);
}
