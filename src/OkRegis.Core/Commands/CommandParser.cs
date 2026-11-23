using OkRegis.Core.Languages;
using OkRegis.Core.Settings;
using OkRegis.Core.Text;

namespace OkRegis.Core.Commands;

/// <summary>Transforme une phrase entendue ("ok régis mets lol") en Command, dans la langue choisie.</summary>
public class CommandParser
{
    /// <summary>Où chercher la cible d'un verbe : scènes, sources de la scène affichée, ou sources audio.</summary>
    public enum TargetPool { Scenes, Sources, Audio }

    private readonly AppSettings _settings;
    private readonly LanguagePack _pack;

    public CommandParser(AppSettings settings, LanguagePack? pack = null)
    {
        _settings = settings;
        _pack = pack ?? LanguagePack.For(settings.Language);
    }

    public ParseResult Parse(string heardText, ObsSnapshot snapshot)
    {
        var words = Normalizer.Normalize(heardText).Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();

        // 1. mot d'appel : [ok|hey] <nom>  (le nom peut aussi être dit en formes parlées : "R2" → "r deux")
        var start = words.Count > 0 && _pack.WakeWords.Contains(words[0]) ? 1 : 0;
        var wakeLength = MatchWakeWord(words, start);
        if (wakeLength == 0) return new ParseResult(ParseStatus.NoWakeWord, null);
        var rest = words.Skip(start + wakeLength).ToList();
        var restText = string.Join(' ', rest);

        // 2. enregistrement / stream (phrase fixe)
        foreach (var (phrase, kind) in _pack.FixedCommands)
        {
            if (restText != phrase) continue;
            return _settings.IsEnabled(kind)
                ? new ParseResult(ParseStatus.Ok, new Command(kind, null))
                : new ParseResult(ParseStatus.Disabled, null);
        }

        // 3. verbe + cible (le verbe le plus long d'abord)
        foreach (var (verb, kind, pool) in _pack.Verbs.OrderByDescending(v => v.Phrase.Length))
        {
            var verbWords = verb.Split(' ');
            if (rest.Count < verbWords.Length || !rest.Take(verbWords.Length).SequenceEqual(verbWords)) continue;
            if (!_settings.IsEnabled(kind)) return new ParseResult(ParseStatus.Disabled, null);

            var targetWords = rest.Skip(verbWords.Length).SkipWhile(_pack.Articles.Contains).ToList();
            var candidates = pool switch
            {
                TargetPool.Scenes => snapshot.Scenes,
                TargetPool.Sources => snapshot.SourcesInCurrentScene,
                _ => snapshot.AudioSources,
            };
            var target = FindTarget(string.Join(' ', targetWords), candidates);
            return target is null
                ? new ParseResult(ParseStatus.UnknownTarget, null)
                : new ParseResult(ParseStatus.Ok, new Command(kind, target));
        }
        return new ParseResult(ParseStatus.UnknownVerb, null);
    }

    // Renvoie le nombre de mots du nom de l'assistant au début de la phrase (0 = pas trouvé).
    private int MatchWakeWord(List<string> words, int start)
    {
        var forms = SpokenForms.For(_settings.AssistantName, _settings.Language)
            .Select(f => f.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Where(f => f.Length > 0).ToList();
        // D'abord une correspondance exacte (la plus longue), ensuite une correspondance avec petite faute.
        foreach (var exact in new[] { true, false })
            foreach (var f in forms.OrderByDescending(f => f.Length))
            {
                if (words.Count < start + f.Length) continue;
                var heard = string.Join(' ', words.Skip(start).Take(f.Length));
                var form = string.Join(' ', f);
                if (exact ? heard == form : Normalizer.IsClose(heard, form)) return f.Length;
            }
        return 0;
    }

    // Cherche la cible : surnom, nom exact, nom proche, puis mot contenu ("micro" dans "Micro/Aux").
    private string? FindTarget(string spoken, IReadOnlyList<string> candidates)
    {
        if (spoken.Length == 0) return null;

        foreach (var (nick, real) in _settings.Nicknames)
            if (SpokenForms.For(nick, _settings.Language).Contains(spoken))
            {
                var hit = candidates.FirstOrDefault(c => Normalizer.Normalize(c) == Normalizer.Normalize(real));
                if (hit is not null) return hit;
            }

        var normalized = candidates.Select(c => (Real: c, Norm: Normalizer.Normalize(c))).Where(c => c.Norm.Length > 0).ToList();
        return normalized.FirstOrDefault(c => c.Norm == spoken).Real
            ?? normalized.FirstOrDefault(c => Normalizer.IsClose(c.Norm, spoken)).Real
            ?? normalized.FirstOrDefault(c => SpokenForms.For(c.Real, _settings.Language).Contains(spoken)).Real // "c s deux" → CS2
            ?? (_pack.MicAliases.Contains(spoken) ? normalized.FirstOrDefault(c => c.Norm.Split(' ').Any(_pack.MicTokens.Contains)).Real : null) // « le micro » = Mic/Aux
            ?? normalized.FirstOrDefault(c => c.Norm.Split(' ').Contains(spoken)).Real;
    }
}
