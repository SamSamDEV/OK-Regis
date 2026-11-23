using System.Text;
using System.Text.RegularExpressions;

namespace OkRegis.Core.Text;

/// <summary>Propose comment un nom s'ENTEND à voix haute (ex. "CS2" → "c s deux").</summary>
public static class SpokenForms
{
    private static readonly string[] Unites =
    {
        "zéro", "un", "deux", "trois", "quatre", "cinq", "six", "sept", "huit", "neuf", "dix",
        "onze", "douze", "treize", "quatorze", "quinze", "seize", "dix sept", "dix huit", "dix neuf"
    };
    private static readonly string[] Dizaines =
        { "", "", "vingt", "trente", "quarante", "cinquante", "soixante" };

    /// <summary>Liste des variantes parlées possibles (normalisées, sans doublon).</summary>
    public static IReadOnlyList<string> For(string name)
    {
        var norm = Normalizer.Normalize(name);
        var result = new List<string>();
        if (norm.Length == 0) return result;

        void Add(string s) { s = Normalizer.Normalize(s); if (s.Length > 0 && !result.Contains(s)) result.Add(s); }

        Add(norm);
        Add(Build(norm, spellShortWords: false, spellNextToDigits: false));
        Add(Build(norm, spellShortWords: false, spellNextToDigits: true));
        Add(Build(norm, spellShortWords: true, spellNextToDigits: false));
        return result;
    }

    // Découpe en suites de lettres / suites de chiffres, puis transforme selon les options.
    private static string Build(string norm, bool spellShortWords, bool spellNextToDigits)
    {
        var parts = new List<string>();
        foreach (var token in norm.Split(' '))
        {
            var runs = Regex.Matches(token, "[0-9]+|[^0-9]+").Select(m => m.Value).ToList();
            var hasDigits = runs.Any(r => IsAsciiDigit(r[0]));
            foreach (var run in runs)
            {
                if (IsAsciiDigit(run[0])) { parts.Add(NumberToWords(run)); continue; }
                var spell = (spellShortWords && run.Length <= 3) || (spellNextToDigits && hasDigits && runs.Count > 1);
                parts.Add(spell ? string.Join(' ', run.Select(c => c.ToString())) : run);
            }
        }
        return string.Join(' ', parts);
    }

    // Seuls les chiffres 0-9 sont convertis (les autres chiffres Unicode restent des caractères ordinaires).
    private static bool IsAsciiDigit(char c) => c is >= '0' and <= '9';

    private static string NumberToWords(string digits)
    {
        if (digits.Length > 2 || digits[0] == '0' && digits.Length > 1)
            return string.Join(' ', digits.Select(d => Unites[d - '0'])); // chiffre par chiffre
        var n = int.Parse(digits);
        if (n < 20) return Unites[n];
        var d = n / 10; var u = n % 10;
        if (d == 7 || d == 9) // 70-79 et 90-99 : soixante-dix / quatre-vingt-dix
        {
            var baseWord = d == 7 ? "soixante" : "quatre vingt";
            var reste = 10 + u;
            if (d == 7 && u == 1) return "soixante et onze";
            return $"{baseWord} {Unites[reste]}";
        }
        if (d == 8) return u == 0 ? "quatre vingts" : $"quatre vingt {Unites[u]}";
        if (u == 0) return Dizaines[d];
        return u == 1 ? $"{Dizaines[d]} et un" : $"{Dizaines[d]} {Unites[u]}";
    }
}
