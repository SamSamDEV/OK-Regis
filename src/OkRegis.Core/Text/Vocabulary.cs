namespace OkRegis.Core.Text;

/// <summary>Les mots que le modèle Vosk connaît. Vosk n'entend QUE ces mots.</summary>
public class Vocabulary
{
    // clé = mot normalisé (sans accent), valeur = mot exact du modèle (avec accents)
    private readonly Dictionary<string, string> _words = new();

    public Vocabulary(IEnumerable<string> words)
    {
        foreach (var w in words)
        {
            var key = Normalizer.Normalize(w);
            if (key.Length > 0 && !_words.ContainsKey(key)) _words[key] = w;
        }
    }

    /// <summary>Lit le fichier graph/words.txt du modèle Vosk (un mot par ligne, suivi d'un numéro).</summary>
    public static Vocabulary FromWordsFile(string path) =>
        new(File.ReadLines(path).Select(l => l.Split(' ', '\t')[0]).Where(w => w.Length > 0));

    public bool Contains(string word) => _words.ContainsKey(Normalizer.Normalize(word));

    /// <summary>Transforme une phrase normalisée en mots exacts du modèle (mots inconnus ignorés).</summary>
    public IReadOnlyList<string> Resolve(string spokenForm) =>
        spokenForm.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                  .Where(w => _words.ContainsKey(w)).Select(w => _words[w]).ToList();

    /// <summary>Vrai si au moins une variante parlée du nom est entièrement connue du modèle.</summary>
    public bool CanSpeak(string name, out string? spokenForm)
    {
        foreach (var form in SpokenForms.For(name))
        {
            if (form.Split(' ').All(_words.ContainsKey)) { spokenForm = form; return true; }
        }
        spokenForm = null;
        return false;
    }
}
