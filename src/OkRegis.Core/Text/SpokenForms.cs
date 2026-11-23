using System.Text.RegularExpressions;
using OkRegis.Core.Languages;
using OkRegis.Core.Settings;

namespace OkRegis.Core.Text;

/// <summary>Propose comment un nom s'ENTEND à voix haute (ex. "CS2" → "c s deux" en français, "c s two" en anglais).</summary>
public static class SpokenForms
{
    /// <summary>Liste des variantes parlées possibles (normalisées, sans doublon).</summary>
    public static IReadOnlyList<string> For(string name, Language language = Language.French)
    {
        var norm = Normalizer.Normalize(name);
        var result = new List<string>();
        if (norm.Length == 0) return result;
        var numbers = LanguagePack.For(language).TwoDigitNumber;

        void Add(string s) { s = Normalizer.Normalize(s); if (s.Length > 0 && !result.Contains(s)) result.Add(s); }

        Add(norm);
        Add(Build(norm, numbers, spellShortWords: false, spellNextToDigits: false));
        Add(Build(norm, numbers, spellShortWords: false, spellNextToDigits: true));
        Add(Build(norm, numbers, spellShortWords: true, spellNextToDigits: false));
        return result;
    }

    // Découpe en suites de lettres / suites de chiffres, puis transforme selon les options.
    private static string Build(string norm, Func<int, string> numbers, bool spellShortWords, bool spellNextToDigits)
    {
        var parts = new List<string>();
        foreach (var token in norm.Split(' '))
        {
            var runs = Regex.Matches(token, "[0-9]+|[^0-9]+").Select(m => m.Value).ToList();
            var hasDigits = runs.Any(r => IsAsciiDigit(r[0]));
            foreach (var run in runs)
            {
                if (IsAsciiDigit(run[0])) { parts.Add(NumberToWords(run, numbers)); continue; }
                var spell = (spellShortWords && run.Length <= 3) || (spellNextToDigits && hasDigits && runs.Count > 1);
                parts.Add(spell ? string.Join(' ', run.Select(c => c.ToString())) : run);
            }
        }
        return string.Join(' ', parts);
    }

    // Seuls les chiffres 0-9 sont convertis (les autres chiffres Unicode restent des caractères ordinaires).
    private static bool IsAsciiDigit(char c) => c is >= '0' and <= '9';

    // 0 à 99 : le nombre en toutes lettres ; au-delà (ou avec un zéro devant) : chiffre par chiffre.
    private static string NumberToWords(string digits, Func<int, string> numbers)
    {
        if (digits.Length > 2 || digits[0] == '0' && digits.Length > 1)
            return string.Join(' ', digits.Select(d => numbers(d - '0')));
        return numbers(int.Parse(digits));
    }
}
