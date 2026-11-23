namespace OkRegis.Core.Settings;

/// <summary>Les types de commandes que Régis sait faire. Chacun peut être activé/désactivé.</summary>
public enum CommandKind
{
    SwitchScene, ShowSource, HideSource, Mute, Unmute, VolumeUp, VolumeDown,
    RecordStart, RecordStop, RecordPause, StreamStart, StreamStop
}

/// <summary>Comment Régis répond : voix Windows, fichiers perso, simple bip ou silence.</summary>
public enum ResponseMode { WindowsVoice, CustomFiles, Beep, Silent }

/// <summary>La langue de l'appli : interface ET reconnaissance vocale.</summary>
public enum Language { French, English, Spanish }

/// <summary>Apparence : suivre Windows, clair, ou sombre (thème « nuit »).</summary>
public enum ThemeMode { System, Light, Dark }

/// <summary>Tous les réglages, enregistrés dans %AppData%\OK-Regis\settings.json.</summary>
public class AppSettings
{
    public Language Language { get; set; } = Language.French;
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

    /// <summary>Phrases PERSONNALISÉES (clé d'action → phrases). Vide = les phrases par défaut de la langue choisie.</summary>
    public Dictionary<string, List<string>> Responses { get; set; } = new();

    /// <summary>clé d'action → chemin d'un fichier WAV/MP3 perso.</summary>
    public Dictionary<string, string> CustomResponseFiles { get; set; } = new();

    /// <summary>Une commande absente de la liste est activée, sauf le stream.</summary>
    public bool IsEnabled(CommandKind kind) =>
        EnabledCommands.TryGetValue(kind, out var on) ? on
        : kind is not (CommandKind.StreamStart or CommandKind.StreamStop);
}
