using System.Text.Json;
using OkRegis.Core.Commands;
using OkRegis.Core.Settings;
using OkRegis.Core.Text;

namespace OkRegis.Core.Speech;

/// <summary>Phrases = mots à donner à Vosk. Unspeakable = noms que Vosk ne pourra pas entendre.</summary>
public record GrammarResult(IReadOnlyList<string> Phrases, IReadOnlyList<string> Unspeakable);

/// <summary>Construit la liste (limitée) de mots que Vosk a le droit de reconnaître.</summary>
public static class GrammarBuilder
{
    public static GrammarResult Build(AppSettings settings, ObsSnapshot obs, Vocabulary vocab)
    {
        var words = new List<string>();
        var unspeakable = new List<string>();

        void AddName(string name)
        {
            if (vocab.CanSpeak(name, out var form)) words.AddRange(vocab.Resolve(form!));
            else if (!unspeakable.Contains(name)) unspeakable.Add(name);
        }

        AddName(settings.AssistantName);
        foreach (var w in CommandParser.FixedWords) words.AddRange(vocab.Resolve(w));
        var names = obs.Scenes.Concat(obs.SourcesInCurrentScene).Concat(obs.AudioSources).Concat(settings.Nicknames.Keys);
        foreach (var name in names) AddName(name);

        words.Add("[unk]");
        return new GrammarResult(words.Distinct().ToList(), unspeakable);
    }

    /// <summary>Le format attendu par Vosk : un tableau JSON de mots.</summary>
    public static string ToVoskJson(GrammarResult r) => JsonSerializer.Serialize(r.Phrases,
        new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
}
