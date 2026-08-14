namespace CriaCerto.Modules.Tenancy.Application.Domain;

public static class CnpjNormalizer
{
    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        return new string(input.Where(char.IsDigit).ToArray());
    }

    public static bool IsValidCnpjOrCpf(string? input)
    {
        var digits = Normalize(input);
        return digits.Length is 11 or 14;
    }
}
