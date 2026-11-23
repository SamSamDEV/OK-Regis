namespace OkRegis.Core.Settings;

/// <summary>Les types de commandes que Régis sait faire. Chacun peut être activé/désactivé.</summary>
public enum CommandKind
{
    SwitchScene, ShowSource, HideSource, Mute, Unmute, VolumeUp, VolumeDown,
    RecordStart, RecordStop, RecordPause, StreamStart, StreamStop
}

/// <summary>Comment Régis répond : voix Windows, fichiers perso, simple bip ou silence.</summary>
public enum ResponseMode { WindowsVoice, CustomFiles, Beep, Silent }

/// <summary>Apparence : suivre Windows, clair, ou sombre (thème « nuit »).</summary>
public enum ThemeMode { System, Light, Dark }

/// <summary>Tous les réglages, enregistrés dans %AppData%\OK-Regis\settings.json.</summary>
public class AppSettings
{
    public string AssistantName { get; set; } = "régis";
    public string ObsHost { get; set; } = "localhost";
    public int ObsPort { get; set; } = 4455;
    public string ObsPasswordEncrypted { get; set; } = "";
    public string? MicDeviceName { get; set; }
    public string? OutputDeviceName { get; set; }
    public double VolumeStepDb { get; set; } = 3.0;
    public double MinConfidence { get; set; } = 0.6;
    /// <summary>N'analyser le micro que quand quelqu'un parle (beaucoup moins de processeur).</summary>
    public bool SaveCpuWhenSilent { get; set; } = true;
    public bool StartWithWindows { get; set; } = true;
    public bool FirstRunDone { get; set; } = false;
    public ResponseMode VoiceMode { get; set; } = ResponseMode.WindowsVoice;
    public ThemeMode Theme { get; set; } = ThemeMode.System;

    /// <summary>Le stream est désactivé par défaut (demande de l'utilisateur).</summary>
    public Dictionary<CommandKind, bool> EnabledCommands { get; set; } = new()
    {
        [CommandKind.StreamStart] = false,
        [CommandKind.StreamStop] = false,
    };

    /// <summary>L'ordre des scènes choisi dans l'appli (glisser-déposer). Ne change rien dans OBS.</summary>
    public List<string> SceneOrder { get; set; } = new();

    /// <summary>L'ordre des sources par scène (clé = nom de la scène, "(audio)" pour les sources audio).</summary>
    public Dictionary<string, List<string>> SourceOrder { get; set; } = new();

    /// <summary>surnom → vrai nom de scène ou de source dans OBS.</summary>
    public Dictionary<string, string> Nicknames { get; set; } = new();

    /// <summary>clé d'action → phrases possibles ({scène} et {source} sont remplacés).</summary>
    public Dictionary<string, List<string>> Responses { get; set; } = DefaultResponses();

    /// <summary>clé d'action → chemin d'un fichier WAV/MP3 perso.</summary>
    public Dictionary<string, string> CustomResponseFiles { get; set; } = new();

    /// <summary>Une commande absente de la liste est activée, sauf le stream.</summary>
    public bool IsEnabled(CommandKind kind) =>
        EnabledCommands.TryGetValue(kind, out var on) ? on
        : kind is not (CommandKind.StreamStart or CommandKind.StreamStop);

    private static Dictionary<string, List<string>> DefaultResponses() => new()
    {
        ["scene"] = new() { "C'est parti, on passe sur {scène}", "Direction {scène}" },
        ["show"] = new() { "{source} affichée" },
        ["hide"] = new() { "{source} masquée" },
        ["mute"] = new() { "{source} coupé" },
        ["unmute"] = new() { "{source} rétabli" },
        ["volumeUp"] = new() { "{source} plus fort" },
        ["volumeDown"] = new() { "{source} moins fort" },
        ["recordStart"] = new() { "Enregistrement lancé" },
        ["recordStop"] = new() { "Enregistrement arrêté" },
        ["recordPause"] = new() { "Enregistrement en pause" },
        ["streamStart"] = new() { "Le stream démarre" },
        ["streamStop"] = new() { "Le stream s'arrête" },
        ["failed"] = new() { "Je n'ai pas pu le faire" },
    };
}
