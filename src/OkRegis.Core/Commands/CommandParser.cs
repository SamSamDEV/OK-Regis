using OkRegis.Core.Settings;
using OkRegis.Core.Text;

namespace OkRegis.Core.Commands;

/// <summary>Transforme une phrase entendue ("ok régis mets lol") en Command.</summary>
public class CommandParser
{
    /// <summary>Où chercher la cible d'un verbe : scènes, sources de la scène affichée, ou sources audio.</summary>
    public enum TargetPool { Scenes, Sources, Audio }

    // phrase (normalisée) → type de commande + où chercher la cible
    private static readonly (string Verb, CommandKind Kind, TargetPool Pool)[] Verbs =
    {
        ("passe sur", CommandKind.SwitchScene, TargetPool.Scenes),
        ("mets", CommandKind.SwitchScene, TargetPool.Scenes),
        ("met", CommandKind.SwitchScene, TargetPool.Scenes),
        ("scene", CommandKind.SwitchScene, TargetPool.Scenes),
        ("affiche", CommandKind.ShowSource, TargetPool.Sources),
        ("montre", CommandKind.ShowSource, TargetPool.Sources),
        ("cache", CommandKind.HideSource, TargetPool.Sources),
        ("masque", CommandKind.HideSource, TargetPool.Sources),
        ("enleve", CommandKind.HideSource, TargetPool.Sources),
        ("coupe", CommandKind.Mute, TargetPool.Audio),
        ("mute", CommandKind.Mute, TargetPool.Audio),
        ("muet", CommandKind.Mute, TargetPool.Audio),
        ("unmute", CommandKind.Unmute, TargetPool.Audio),
        ("remets", CommandKind.Unmute, TargetPool.Audio),
        ("remet", CommandKind.Unmute, TargetPool.Audio),
        ("retablis", CommandKind.Unmute, TargetPool.Audio),
        ("monte", CommandKind.VolumeUp, TargetPool.Audio),
        ("baisse", CommandKind.VolumeDown, TargetPool.Audio),
    };

    // verbes d'enregistrement / de stream : la "cible" est un mot fixe
    private static readonly (string Verb, string Subject, CommandKind Kind)[] Fixed =
    {
        ("lance", "l enregistrement", CommandKind.RecordStart),
        ("demarre", "l enregistrement", CommandKind.RecordStart),
        ("arrete", "l enregistrement", CommandKind.RecordStop),
        ("stop", "l enregistrement", CommandKind.RecordStop),
        ("pause", "l enregistrement", CommandKind.RecordPause),
        ("lance", "le stream", CommandKind.StreamStart),
        ("demarre", "le stream", CommandKind.StreamStart),
        ("arrete", "le stream", CommandKind.StreamStop),
        ("stop", "le stream", CommandKind.StreamStop),
    };

    /// <summary>Les verbes qui attendent une cible (pour construire la grammaire vocale).</summary>
    public static IReadOnlyList<(string Verb, CommandKind Kind, TargetPool Pool)> TargetVerbs => Verbs;

    /// <summary>Les phrases complètes enregistrement / stream (sans le nom de l'assistant).</summary>
    public static IEnumerable<(string Phrase, CommandKind Kind)> FixedPhrases =>
        Fixed.SelectMany(f => f.Subject == "l enregistrement"
            ? new[] { ($"{f.Verb} l enregistrement", f.Kind), ($"{f.Verb} enregistrement", f.Kind) }
            : new[] { ($"{f.Verb} {f.Subject}", f.Kind) });

    /// <summary>Les mots d'un nom de source qui font penser à un micro ("Mic/Aux", "Micro Blue Yeti"...).</summary>
    public static readonly HashSet<string> MicTokens = new() { "mic", "micro", "microphone", "aux" };

    private static readonly HashSet<string> Articles = new() { "le", "la", "les", "l", "un", "une", "du", "de", "d" };

    /// <summary>Tous les mots-outils que Régis doit pouvoir entendre (verbes, articles, ok/hey...).</summary>
    public static IEnumerable<string> FixedWords =>
        Verbs.Select(v => v.Verb).Concat(Fixed.Select(f => f.Verb + " " + f.Subject))
             .Concat(Articles).Concat(new[] { "ok", "hey", "enregistrement", "stream", "micro" })
             .SelectMany(p => p.Split(' ')).Distinct();

    private readonly AppSettings _settings;
    public CommandParser(AppSettings settings) => _settings = settings;

    public ParseResult Parse(string heardText, ObsSnapshot snapshot)
    {
        var words = Normalizer.Normalize(heardText).Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();

        // 1. mot d'appel : [ok|hey] <nom>  (le nom peut aussi être dit en formes parlées : "R2" → "r deux")
        var start = words.Count > 0 && words[0] is "ok" or "hey" ? 1 : 0;
        var wakeLength = MatchWakeWord(words, start);
        if (wakeLength == 0) return new ParseResult(ParseStatus.NoWakeWord, null);
        var rest = words.Skip(start + wakeLength).ToList();
        var restText = string.Join(' ', rest);

        // 2. enregistrement / stream (phrase fixe)
        foreach (var (verb, subject, kind) in Fixed)
        {
            if (restText == $"{verb} {subject}" || restText == $"{verb} {subject.Replace("l enregistrement", "enregistrement")}")
                return _settings.IsEnabled(kind)
                    ? new ParseResult(ParseStatus.Ok, new Command(kind, null))
                    : new ParseResult(ParseStatus.Disabled, null);
        }

        // 3. verbe + cible (le verbe le plus long d'abord)
        foreach (var (verb, kind, pool) in Verbs.OrderByDescending(v => v.Verb.Length))
        {
            var verbWords = verb.Split(' ');
            if (rest.Count < verbWords.Length || !rest.Take(verbWords.Length).SequenceEqual(verbWords)) continue;
            if (!_settings.IsEnabled(kind)) return new ParseResult(ParseStatus.Disabled, null);

            var targetWords = rest.Skip(verbWords.Length).SkipWhile(Articles.Contains).ToList();
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
        var forms = SpokenForms.For(_settings.AssistantName)
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
            if (SpokenForms.For(nick).Contains(spoken))
            {
                var hit = candidates.FirstOrDefault(c => Normalizer.Normalize(c) == Normalizer.Normalize(real));
                if (hit is not null) return hit;
            }

        var normalized = candidates.Select(c => (Real: c, Norm: Normalizer.Normalize(c))).Where(c => c.Norm.Length > 0).ToList();
        return normalized.FirstOrDefault(c => c.Norm == spoken).Real
            ?? normalized.FirstOrDefault(c => Normalizer.IsClose(c.Norm, spoken)).Real
            ?? normalized.FirstOrDefault(c => SpokenForms.For(c.Real).Contains(spoken)).Real // "c s deux" → CS2
            ?? (spoken is "micro" or "microphone" or "mic" ? normalized.FirstOrDefault(c => c.Norm.Split(' ').Any(MicTokens.Contains)).Real : null) // « le micro » = Mic/Aux
            ?? normalized.FirstOrDefault(c => c.Norm.Split(' ').Contains(spoken)).Real;
    }
}
