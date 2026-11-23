using OkRegis.Core.Settings;

namespace OkRegis.Core.Text;

/// <summary>Les mots que le modèle Vosk connaît. Vosk n'entend QUE ces mots.</summary>
public class Vocabulary
{
    // Accents possibles pour retrouver l'écriture exacte d'un mot du modèle ("regis" → "régis"), selon la langue.
    private static readonly Dictionary<char, string> FrenchAccents = new()
    {
        ['e'] = "eéèêë", ['a'] = "aàâ", ['i'] = "iîï", ['o'] = "oô", ['u'] = "uùû", ['c'] = "cç",
    };
    private static readonly Dictionary<char, string> SpanishAccents = new()
    {
        ['a'] = "aá", ['e'] = "eé", ['i'] = "ií", ['o'] = "oó", ['u'] = "uúü", ['n'] = "nñ",
    };
    private static readonly Dictionary<char, string> NoAccents = new();
    private const int MaxVariants = 2000;

    private readonly Func<string, string?> _lookup;
    private readonly Language _language;
    private readonly Dictionary<string, string?> _cache = new(); // mot normalisé → mot exact du modèle (null = inconnu)

    /// <summary>Vocabulaire fixe (liste de mots exacts, avec accents).</summary>
    public Vocabulary(IEnumerable<string> words, Language language = Language.French)
    {
        _language = language;
        var map = new Dictionary<string, string>();
        foreach (var w in words)
        {
            var key = Normalizer.Normalize(w);
            if (key.Length > 0 && !map.ContainsKey(key)) map[key] = w;
        }
        _lookup = key => map.GetValueOrDefault(key);
    }

    /// <summary>Vocabulaire interrogé mot par mot (ex. Model.FindWord de Vosk : le mot doit être écrit exactement).</summary>
    public Vocabulary(Func<string, bool> exists, Language language = Language.French)
    {
        _language = language;
        _lookup = key => Variants(key, language).FirstOrDefault(exists);
    }

    public bool Contains(string word) => Lookup(Normalizer.Normalize(word)) != null;

    /// <summary>Transforme une phrase normalisée en mots exacts du modèle (mots inconnus ignorés).</summary>
    public IReadOnlyList<string> Resolve(string spokenForm) =>
        spokenForm.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                  .Select(Lookup).Where(w => w != null).Select(w => w!).ToList();

    /// <summary>Vrai si au moins une variante parlée du nom est entièrement connue du modèle.</summary>
    public bool CanSpeak(string name, out string? spokenForm)
    {
        foreach (var form in SpokenForms.For(name, _language))
        {
            if (form.Split(' ').All(w => Lookup(w) != null)) { spokenForm = form; return true; }
        }
        spokenForm = null;
        return false;
    }

    private string? Lookup(string key)
    {
        if (key.Length == 0) return null;
        lock (_cache)
        {
            if (!_cache.TryGetValue(key, out var found)) _cache[key] = found = _lookup(key);
            return found;
        }
    }

    // Le mot tel quel, puis toutes ses écritures accentuées possibles (plafonné pour rester rapide).
    private static IEnumerable<string> Variants(string key, Language language)
    {
        yield return key;
        var accents = language switch { Language.French => FrenchAccents, Language.Spanish => SpanishAccents, _ => NoAccents };
        var options = key.Select(c => accents.TryGetValue(c, out var a) ? a : c.ToString()).ToArray();
        long count = 1;
        foreach (var o in options) { count *= o.Length; if (count > MaxVariants) yield break; }
        var results = new List<string> { "" };
        foreach (var o in options)
            results = results.SelectMany(prefix => o.Select(c => prefix + c)).ToList();
        foreach (var r in results.Where(r => r != key)) yield return r;
    }
}
