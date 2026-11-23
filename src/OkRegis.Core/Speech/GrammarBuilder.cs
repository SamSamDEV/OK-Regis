using System.Text.Json;
using OkRegis.Core.Commands;
using OkRegis.Core.Settings;
using OkRegis.Core.Text;

namespace OkRegis.Core.Speech;

/// <summary>Phrases = phrases complètes données à Vosk. Unspeakable = noms que Vosk ne pourra pas entendre.</summary>
public record GrammarResult(IReadOnlyList<string> Phrases, IReadOnlyList<string> Unspeakable);

/// <summary>
/// Construit la grammaire de Vosk : uniquement des PHRASES COMPLÈTES valides
/// ("ok régis mets lol", "régis coupe spotify"...). Vosk ne peut alors rien entendre d'autre,
/// ce qui rend la reconnaissance bien plus précise qu'une simple liste de mots.
/// </summary>
public static class GrammarBuilder
{
    private static readonly string[] Wakes = { "", "ok ", "hey " };

    public static GrammarResult Build(AppSettings settings, ObsSnapshot obs, Vocabulary vocab)
    {
        var phrases = new List<string>();
        var unspeakable = new List<string>();

        // Le nom de l'assistant, tel que Vosk l'entend.
        string? name = null;
        if (vocab.CanSpeak(settings.AssistantName, out var nameForm)) name = string.Join(' ', vocab.Resolve(nameForm!));
        else unspeakable.Add(settings.AssistantName);

        if (name != null)
        {
            // Pour chaque type de cible : les noms (et surnoms) que Vosk sait dire.
            var pools = new Dictionary<CommandParser.TargetPool, List<string>>
            {
                [CommandParser.TargetPool.Scenes] = SpokenNames(obs.Scenes, settings, vocab, unspeakable),
                [CommandParser.TargetPool.Sources] = SpokenNames(obs.SourcesInCurrentScene, settings, vocab, unspeakable),
                [CommandParser.TargetPool.Audio] = SpokenNames(obs.AudioSources, settings, vocab, unspeakable),
            };

            foreach (var (verb, kind, pool) in CommandParser.TargetVerbs)
            {
                if (!settings.IsEnabled(kind)) continue;
                var verbWords = KnownWords(verb, vocab);
                if (verbWords is null) continue; // un verbe que le modèle ne connaît pas : on ne le propose pas
                foreach (var target in pools[pool])
                {
                    Add(phrases, name, $"{verbWords} {target}");
                    if (pool == CommandParser.TargetPool.Audio) Add(phrases, name, $"{verbWords} le {target}"); // "coupe le micro"
                }
            }
            foreach (var (fixedPhrase, kind) in CommandParser.FixedPhrases)
            {
                if (!settings.IsEnabled(kind)) continue;
                var words = KnownWords(fixedPhrase, vocab);
                if (words != null) Add(phrases, name, words);
            }
        }

        phrases.Add("[unk]");
        return new GrammarResult(phrases.Distinct().ToList(), unspeakable);
    }

    // Ajoute "<nom>", "ok <nom>" et "hey <nom>" devant la commande.
    private static void Add(List<string> phrases, string name, string command)
    {
        foreach (var wake in Wakes) phrases.Add($"{wake}{name} {command}".Trim());
    }

    // Les mots exacts du modèle pour une phrase, ou null si un mot manque.
    private static string? KnownWords(string phrase, Vocabulary vocab)
    {
        var expected = phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        var words = vocab.Resolve(phrase);
        return words.Count == expected ? string.Join(' ', words) : null;
    }

    // Les noms d'un type (+ les surnoms qui y mènent) tels que Vosk les entend ; les autres vont dans "unspeakable".
    private static List<string> SpokenNames(IReadOnlyList<string> names, AppSettings settings, Vocabulary vocab, List<string> unspeakable)
    {
        var result = new List<string>();
        void Try(string spoken)
        {
            if (vocab.CanSpeak(spoken, out var form)) result.Add(string.Join(' ', vocab.Resolve(form!)));
            else if (!unspeakable.Contains(spoken)) unspeakable.Add(spoken);
        }
        // Un mot seul du nom suffit aussi ("chatting" pour "Just Chatting") ; « micro » pour Mic/Aux.
        void TryQuiet(string word)
        {
            if (vocab.CanSpeak(word, out var form)) result.Add(string.Join(' ', vocab.Resolve(form!)));
        }
        foreach (var n in names)
        {
            Try(n);
            var tokens = Normalizer.Normalize(n).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var token in tokens.Where(t => t.Length >= 3 && !t.All(char.IsDigit))) TryQuiet(token);
            if (tokens.Any(CommandParser.MicTokens.Contains)) TryQuiet("micro");
        }
        foreach (var (nick, real) in settings.Nicknames.ToList())
            if (names.Any(n => Normalizer.Normalize(n) == Normalizer.Normalize(real))) Try(nick);
        return result.Distinct().ToList();
    }

    /// <summary>Le format attendu par Vosk : un tableau JSON de phrases.</summary>
    public static string ToVoskJson(GrammarResult r) => JsonSerializer.Serialize(r.Phrases,
        new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
}
