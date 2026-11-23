using OkRegis.Core.Commands;
using OkRegis.Core.Settings;

namespace OkRegis.Core.Languages;

/// <summary>Un verbe de commande : la phrase (normalisée : minuscules, sans accents), son type, et où chercher sa cible.</summary>
public sealed record VerbDef(string Phrase, CommandKind Kind, CommandParser.TargetPool Pool);

/// <summary>
/// Tout ce qui dépend de la langue : verbes, mots d'appel, noms de nombres, réponses par défaut, modèle vocal à télécharger.
/// Les phrases sont écrites sans accents ni ponctuation (comme après Normalizer.Normalize).
/// </summary>
public sealed class LanguagePack
{
    public Language Language { get; init; }
    public string Code { get; init; } = "";
    /// <summary>Nom du modèle vocal Vosk léger pour cette langue.</summary>
    public string ModelFolder { get; init; } = "";
    public string ModelUrl => $"https://alphacephei.com/vosk/models/{ModelFolder}.zip";
    public string DefaultAssistantName { get; init; } = "regis";
    public IReadOnlyList<VerbDef> Verbs { get; init; } = Array.Empty<VerbDef>();
    /// <summary>Phrases complètes (enregistrement, stream) sans le nom de l'assistant.</summary>
    public IReadOnlyList<(string Phrase, CommandKind Kind)> FixedCommands { get; init; } = Array.Empty<(string, CommandKind)>();
    public IReadOnlySet<string> Articles { get; init; } = new HashSet<string>();
    /// <summary>Les mots qui peuvent précéder le nom de l'assistant (« ok », « hey »…).</summary>
    public IReadOnlySet<string> WakeWords { get; init; } = new HashSet<string>();
    /// <summary>Les mots d'un nom de source qui font penser à un micro ("Mic/Aux", "Micro Blue Yeti"…).</summary>
    public IReadOnlySet<string> MicTokens { get; init; } = new HashSet<string>();
    /// <summary>Ce que l'on dit pour désigner le micro (« le micro »).</summary>
    public IReadOnlySet<string> MicAliases { get; init; } = new HashSet<string>();
    public string MicGrammarWord { get; init; } = "mic";
    /// <summary>L'article utilisé dans « coupe le micro » (pour la grammaire).</summary>
    public string GrammarArticle { get; init; } = "";
    /// <summary>Les nombres de 0 à 99 écrits en lettres (normalisés).</summary>
    public Func<int, string> TwoDigitNumber { get; init; } = n => n.ToString();
    public IReadOnlyDictionary<string, string[]> DefaultResponses { get; init; } = new Dictionary<string, string[]>();

    /// <summary>Tous les mots-outils que Régis doit pouvoir entendre (verbes, articles, mots d'appel…).</summary>
    public IEnumerable<string> FixedWords =>
        Verbs.Select(v => v.Phrase).Concat(FixedCommands.Select(f => f.Phrase))
             .Concat(Articles).Concat(WakeWords).Concat(new[] { MicGrammarWord })
             .SelectMany(p => p.Split(' ')).Distinct();

    // ---- accès par langue ----

    public static LanguagePack For(Language language) => language switch
    {
        Language.English => English,
        Language.Spanish => Spanish,
        _ => French,
    };

    private static VerbDef V(string phrase, CommandKind kind, CommandParser.TargetPool pool) => new(phrase, kind, pool);
    private const CommandParser.TargetPool Scenes = CommandParser.TargetPool.Scenes;
    private const CommandParser.TargetPool Sources = CommandParser.TargetPool.Sources;
    private const CommandParser.TargetPool Audio = CommandParser.TargetPool.Audio;

    // ---- français ----

    public static readonly LanguagePack French = new()
    {
        Language = Language.French, Code = "fr", ModelFolder = "vosk-model-small-fr-0.22", DefaultAssistantName = "régis",
        Verbs = new[]
        {
            V("passe sur", CommandKind.SwitchScene, Scenes), V("mets", CommandKind.SwitchScene, Scenes), V("met", CommandKind.SwitchScene, Scenes), V("scene", CommandKind.SwitchScene, Scenes),
            V("affiche", CommandKind.ShowSource, Sources), V("montre", CommandKind.ShowSource, Sources),
            V("cache", CommandKind.HideSource, Sources), V("masque", CommandKind.HideSource, Sources), V("enleve", CommandKind.HideSource, Sources),
            V("coupe", CommandKind.Mute, Audio), V("mute", CommandKind.Mute, Audio), V("muet", CommandKind.Mute, Audio),
            V("unmute", CommandKind.Unmute, Audio), V("un mute", CommandKind.Unmute, Audio), V("remets", CommandKind.Unmute, Audio), V("remet", CommandKind.Unmute, Audio), V("retablis", CommandKind.Unmute, Audio),
            V("monte", CommandKind.VolumeUp, Audio), V("baisse", CommandKind.VolumeDown, Audio),
        },
        FixedCommands = new[]
        {
            ("lance l enregistrement", CommandKind.RecordStart), ("lance enregistrement", CommandKind.RecordStart),
            ("demarre l enregistrement", CommandKind.RecordStart), ("demarre enregistrement", CommandKind.RecordStart),
            ("arrete l enregistrement", CommandKind.RecordStop), ("arrete enregistrement", CommandKind.RecordStop),
            ("stop l enregistrement", CommandKind.RecordStop), ("stop enregistrement", CommandKind.RecordStop),
            ("pause l enregistrement", CommandKind.RecordPause), ("pause enregistrement", CommandKind.RecordPause),
            ("lance le stream", CommandKind.StreamStart), ("demarre le stream", CommandKind.StreamStart),
            ("arrete le stream", CommandKind.StreamStop), ("stop le stream", CommandKind.StreamStop),
        },
        Articles = new HashSet<string> { "le", "la", "les", "l", "un", "une", "du", "de", "d" },
        WakeWords = new HashSet<string> { "ok", "hey" },
        MicTokens = new HashSet<string> { "mic", "micro", "microphone", "aux" },
        MicAliases = new HashSet<string> { "micro", "microphone", "mic" }, MicGrammarWord = "micro", GrammarArticle = "le",
        TwoDigitNumber = FrenchNumber,
        DefaultResponses = new Dictionary<string, string[]>
        {
            ["scene"] = new[] { "C'est parti, on passe sur {scène}", "Direction {scène}" },
            ["show"] = new[] { "{source} affichée" }, ["hide"] = new[] { "{source} masquée" },
            ["mute"] = new[] { "{source} coupé" }, ["unmute"] = new[] { "{source} rétabli" },
            ["volumeUp"] = new[] { "{source} plus fort" }, ["volumeDown"] = new[] { "{source} moins fort" },
            ["recordStart"] = new[] { "Enregistrement lancé" }, ["recordStop"] = new[] { "Enregistrement arrêté" }, ["recordPause"] = new[] { "Enregistrement en pause" },
            ["streamStart"] = new[] { "Le stream démarre" }, ["streamStop"] = new[] { "Le stream s'arrête" },
            ["failed"] = new[] { "Je n'ai pas pu le faire" },
        },
    };

    private static readonly string[] FrUnits =
    {
        "zero", "un", "deux", "trois", "quatre", "cinq", "six", "sept", "huit", "neuf", "dix",
        "onze", "douze", "treize", "quatorze", "quinze", "seize", "dix sept", "dix huit", "dix neuf",
    };
    private static readonly string[] FrTens = { "", "", "vingt", "trente", "quarante", "cinquante", "soixante" };

    private static string FrenchNumber(int n)
    {
        if (n < 20) return FrUnits[n];
        var d = n / 10; var u = n % 10;
        if (d == 7 || d == 9)
        {
            if (d == 7 && u == 1) return "soixante et onze";
            return $"{(d == 7 ? "soixante" : "quatre vingt")} {FrUnits[10 + u]}";
        }
        if (d == 8) return u == 0 ? "quatre vingts" : $"quatre vingt {FrUnits[u]}";
        if (u == 0) return FrTens[d];
        return u == 1 ? $"{FrTens[d]} et un" : $"{FrTens[d]} {FrUnits[u]}";
    }

    // ---- anglais ----

    public static readonly LanguagePack English = new()
    {
        Language = Language.English, Code = "en", ModelFolder = "vosk-model-small-en-us-0.15", DefaultAssistantName = "regis",
        Verbs = new[]
        {
            V("switch to", CommandKind.SwitchScene, Scenes), V("go to", CommandKind.SwitchScene, Scenes), V("change to", CommandKind.SwitchScene, Scenes), V("scene", CommandKind.SwitchScene, Scenes),
            V("show", CommandKind.ShowSource, Sources), V("display", CommandKind.ShowSource, Sources), V("enable", CommandKind.ShowSource, Sources),
            V("hide", CommandKind.HideSource, Sources), V("remove", CommandKind.HideSource, Sources), V("disable", CommandKind.HideSource, Sources),
            V("mute", CommandKind.Mute, Audio), V("unmute", CommandKind.Unmute, Audio), V("un mute", CommandKind.Unmute, Audio),
            V("raise", CommandKind.VolumeUp, Audio), V("increase", CommandKind.VolumeUp, Audio), V("turn up", CommandKind.VolumeUp, Audio),
            V("lower", CommandKind.VolumeDown, Audio), V("decrease", CommandKind.VolumeDown, Audio), V("turn down", CommandKind.VolumeDown, Audio),
        },
        FixedCommands = new[]
        {
            ("start recording", CommandKind.RecordStart), ("start the recording", CommandKind.RecordStart), ("begin recording", CommandKind.RecordStart),
            ("stop recording", CommandKind.RecordStop), ("stop the recording", CommandKind.RecordStop),
            ("pause recording", CommandKind.RecordPause), ("pause the recording", CommandKind.RecordPause),
            ("start streaming", CommandKind.StreamStart), ("start the stream", CommandKind.StreamStart), ("start stream", CommandKind.StreamStart),
            ("stop streaming", CommandKind.StreamStop), ("stop the stream", CommandKind.StreamStop), ("stop stream", CommandKind.StreamStop),
        },
        Articles = new HashSet<string> { "the", "a", "an", "my" },
        WakeWords = new HashSet<string> { "ok", "hey" },
        MicTokens = new HashSet<string> { "mic", "microphone", "aux" },
        MicAliases = new HashSet<string> { "mic", "microphone" }, MicGrammarWord = "mic", GrammarArticle = "the",
        TwoDigitNumber = EnglishNumber,
        DefaultResponses = new Dictionary<string, string[]>
        {
            ["scene"] = new[] { "On it, switching to {scene}", "Going to {scene}" },
            ["show"] = new[] { "{source} shown" }, ["hide"] = new[] { "{source} hidden" },
            ["mute"] = new[] { "{source} muted" }, ["unmute"] = new[] { "{source} unmuted" },
            ["volumeUp"] = new[] { "{source} louder" }, ["volumeDown"] = new[] { "{source} quieter" },
            ["recordStart"] = new[] { "Recording started" }, ["recordStop"] = new[] { "Recording stopped" }, ["recordPause"] = new[] { "Recording paused" },
            ["streamStart"] = new[] { "The stream is starting" }, ["streamStop"] = new[] { "The stream is stopping" },
            ["failed"] = new[] { "I couldn't do that" },
        },
    };

    private static readonly string[] EnUnits =
    {
        "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve",
        "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen",
    };
    private static readonly string[] EnTens = { "", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety" };

    private static string EnglishNumber(int n)
        => n < 20 ? EnUnits[n] : n % 10 == 0 ? EnTens[n / 10] : $"{EnTens[n / 10]} {EnUnits[n % 10]}";

    // ---- espagnol ----

    public static readonly LanguagePack Spanish = new()
    {
        Language = Language.Spanish, Code = "es", ModelFolder = "vosk-model-small-es-0.42", DefaultAssistantName = "regis",
        Verbs = new[]
        {
            V("cambia a", CommandKind.SwitchScene, Scenes), V("pasa a", CommandKind.SwitchScene, Scenes), V("ve a", CommandKind.SwitchScene, Scenes), V("escena", CommandKind.SwitchScene, Scenes),
            V("muestra", CommandKind.ShowSource, Sources), V("enciende", CommandKind.ShowSource, Sources), V("activa", CommandKind.ShowSource, Sources),
            V("oculta", CommandKind.HideSource, Sources), V("esconde", CommandKind.HideSource, Sources), V("quita", CommandKind.HideSource, Sources), V("apaga", CommandKind.HideSource, Sources),
            V("silencia", CommandKind.Mute, Audio), V("mute", CommandKind.Mute, Audio),
            V("reactiva", CommandKind.Unmute, Audio), V("unmute", CommandKind.Unmute, Audio), V("un mute", CommandKind.Unmute, Audio),
            V("sube", CommandKind.VolumeUp, Audio), V("aumenta", CommandKind.VolumeUp, Audio),
            V("baja", CommandKind.VolumeDown, Audio), V("reduce", CommandKind.VolumeDown, Audio),
        },
        FixedCommands = new[]
        {
            // Le modèle espagnol léger ne connaît pas « grabación » : on utilise « grabar », « grabadora » et « transmitir ».
            ("graba", CommandKind.RecordStart), ("empieza a grabar", CommandKind.RecordStart), ("comienza a grabar", CommandKind.RecordStart), ("inicia la grabadora", CommandKind.RecordStart),
            ("para de grabar", CommandKind.RecordStop), ("deja de grabar", CommandKind.RecordStop), ("para la grabadora", CommandKind.RecordStop),
            ("pausa la grabadora", CommandKind.RecordPause), ("pon en pausa la grabadora", CommandKind.RecordPause),
            ("inicia el stream", CommandKind.StreamStart), ("empieza el stream", CommandKind.StreamStart), ("empieza a transmitir", CommandKind.StreamStart), ("comienza a transmitir", CommandKind.StreamStart),
            ("para el stream", CommandKind.StreamStop), ("deten el stream", CommandKind.StreamStop), ("para de transmitir", CommandKind.StreamStop), ("deja de transmitir", CommandKind.StreamStop),
        },
        Articles = new HashSet<string> { "el", "la", "los", "las", "un", "una", "mi", "de", "del" },
        WakeWords = new HashSet<string> { "ok", "oye", "hey" },
        MicTokens = new HashSet<string> { "mic", "micro", "microfono", "aux" },
        MicAliases = new HashSet<string> { "micro", "microfono", "mic" }, MicGrammarWord = "micro", GrammarArticle = "el",
        TwoDigitNumber = SpanishNumber,
        DefaultResponses = new Dictionary<string, string[]>
        {
            ["scene"] = new[] { "Vamos a {escena}", "Cambiando a {escena}" },
            ["show"] = new[] { "{fuente} visible" }, ["hide"] = new[] { "{fuente} oculta" },
            ["mute"] = new[] { "{fuente} silenciada" }, ["unmute"] = new[] { "{fuente} activada" },
            ["volumeUp"] = new[] { "{fuente} más alto" }, ["volumeDown"] = new[] { "{fuente} más bajo" },
            ["recordStart"] = new[] { "Grabación iniciada" }, ["recordStop"] = new[] { "Grabación detenida" }, ["recordPause"] = new[] { "Grabación en pausa" },
            ["streamStart"] = new[] { "Empieza el stream" }, ["streamStop"] = new[] { "Se detiene el stream" },
            ["failed"] = new[] { "No he podido hacerlo" },
        },
    };

    private static readonly string[] EsUnits =
    {
        "cero", "uno", "dos", "tres", "cuatro", "cinco", "seis", "siete", "ocho", "nueve", "diez", "once", "doce",
        "trece", "catorce", "quince", "dieciseis", "diecisiete", "dieciocho", "diecinueve", "veinte",
        "veintiuno", "veintidos", "veintitres", "veinticuatro", "veinticinco", "veintiseis", "veintisiete", "veintiocho", "veintinueve",
    };
    private static readonly string[] EsTens = { "", "", "", "treinta", "cuarenta", "cincuenta", "sesenta", "setenta", "ochenta", "noventa" };

    private static string SpanishNumber(int n)
        => n <= 29 ? EsUnits[n] : n % 10 == 0 ? EsTens[n / 10] : $"{EsTens[n / 10]} y {EsUnits[n % 10]}";
}
