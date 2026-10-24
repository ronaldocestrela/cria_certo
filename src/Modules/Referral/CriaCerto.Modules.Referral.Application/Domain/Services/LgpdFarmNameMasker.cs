using System.Text;

namespace CriaCerto.Modules.Referral.Application.Domain.Services;

public static class LgpdFarmNameMasker
{
    private const string FallbackMaskedName = "Fazenda Confidencial";

    /// <summary>
    /// Aplica a regra de mascaramento LGPD (Exemplo 3):
    /// Para cada palavra com mais de 2 letras, preserva a primeira e a última letra,
    /// substituindo as letras intermediárias por asteriscos.
    /// Palavras com 2 ou menos letras são preservadas.
    /// Exemplo: "Fazenda Santa Maria" -> "F*****a S***a M***a".
    /// </summary>
    public static string Mask(string? farmName)
    {
        if (string.IsNullOrWhiteSpace(farmName))
        {
            return FallbackMaskedName;
        }

        var words = farmName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return FallbackMaskedName;
        }

        var sb = new StringBuilder();
        for (int i = 0; i < words.Length; i++)
        {
            if (i > 0)
            {
                sb.Append(' ');
            }

            var word = words[i];
            sb.Append(MaskWord(word));
        }

        return sb.ToString();
    }

    private static string MaskWord(string word)
    {
        if (word.Length <= 2)
        {
            return word;
        }

        char firstChar = word[0];
        char lastChar = word[^1];
        int maskedLength = word.Length - 2;

        return $"{firstChar}{new string('*', maskedLength)}{lastChar}";
    }
}
