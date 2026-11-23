using System.Globalization;
using System.Text;

namespace OkRegis.Core.Text;

/// <summary>Outils pour comparer des noms "à la souplesse humaine" (sans accents, sans casse, sans ponctuation).</summary>
public static class Normalizer
{
    /// <summary>Minuscules, sans accents, tout ce qui n'est pas lettre/chiffre devient un espace, espaces réduits.</summary>
    public static string Normalize(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";
        var decomposed = input.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat == UnicodeCategory.NonSpacingMark) continue; // on enlève les accents
            sb.Append(char.IsLetterOrDigit(c) ? c : ' ');
        }
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>Distance de Levenshtein : nombre minimal de modifications pour passer de a à b.</summary>
    public static int Distance(string a, string b)
    {
        var prev = new int[b.Length + 1];
        var curr = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) prev[j] = j;
        for (var i = 1; i <= a.Length; i++)
        {
            curr[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cout = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[j] = Math.Min(Math.Min(curr[j - 1] + 1, prev[j] + 1), prev[j - 1] + cout);
            }
            (prev, curr) = (curr, prev);
        }
        return prev[b.Length];
    }

    /// <summary>Vrai si les deux textes se ressemblent (1 faute max pour les mots courts, 2 sinon).</summary>
    public static bool IsClose(string a, string b)
    {
        var tolerance = Math.Min(a.Length, b.Length) <= 4 ? 1 : 2;
        return Distance(a, b) <= tolerance;
    }
}
