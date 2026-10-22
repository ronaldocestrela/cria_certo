namespace CriaCerto.Modules.Referral.Application.Domain.Services;

public static class LegalDocumentValidator
{
    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        return new string(input.Where(char.IsDigit).ToArray());
    }

    public static bool IsValid(string? input)
    {
        var digits = Normalize(input);
        if (digits.Length == 11)
            return IsValidCpf(digits);
        if (digits.Length == 14)
            return IsValidCnpj(digits);

        return false;
    }

    private static bool IsValidCpf(string cpf)
    {
        if (cpf.Distinct().Count() == 1)
            return false;

        var tempCpf = cpf[..9];
        var sum = 0;

        for (var i = 0; i < 9; i++)
            sum += int.Parse(tempCpf[i].ToString()) * (10 - i);

        var remainder = sum % 11;
        var digit1 = remainder < 2 ? 0 : 11 - remainder;

        tempCpf += digit1;
        sum = 0;

        for (var i = 0; i < 10; i++)
            sum += int.Parse(tempCpf[i].ToString()) * (11 - i);

        remainder = sum % 11;
        var digit2 = remainder < 2 ? 0 : 11 - remainder;

        return cpf.EndsWith($"{digit1}{digit2}");
    }

    private static bool IsValidCnpj(string cnpj)
    {
        if (cnpj.Distinct().Count() == 1)
            return false;

        int[] multiplier1 = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        int[] multiplier2 = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

        var tempCnpj = cnpj[..12];
        var sum = 0;

        for (var i = 0; i < 12; i++)
            sum += int.Parse(tempCnpj[i].ToString()) * multiplier1[i];

        var remainder = sum % 11;
        var digit1 = remainder < 2 ? 0 : 11 - remainder;

        tempCnpj += digit1;
        sum = 0;

        for (var i = 0; i < 13; i++)
            sum += int.Parse(tempCnpj[i].ToString()) * multiplier2[i];

        remainder = sum % 11;
        var digit2 = remainder < 2 ? 0 : 11 - remainder;

        return cnpj.EndsWith($"{digit1}{digit2}");
    }
}
