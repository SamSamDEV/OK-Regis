using OkRegis.Core.Obs;
using OkRegis.Core.Settings;

namespace OkRegis.Core.Commands;

/// <summary>Résultat d'une commande. ActionKey sert à choisir la phrase de réponse.</summary>
public record ExecResult(bool Success, string ActionKey, string? Name);

/// <summary>Envoie une Command à OBS. Ne lève jamais d'exception : une panne devient un échec.</summary>
public class CommandExecutor
{
    private readonly IObsClient _obs;
    private readonly AppSettings _settings;

    public CommandExecutor(IObsClient obs, AppSettings settings) { _obs = obs; _settings = settings; }

    public async Task<ExecResult> ExecuteAsync(Command c, ObsSnapshot snap)
    {
        var key = ActionKey(c.Kind);
        if (!_settings.IsEnabled(c.Kind)) return new ExecResult(false, "failed", c.Target);
        try
        {
            var target = c.Target ?? "";
            switch (c.Kind)
            {
                case CommandKind.SwitchScene: await _obs.SetCurrentSceneAsync(target); break;
                case CommandKind.ShowSource or CommandKind.HideSource:
                    if (snap.CurrentScene is null) return new ExecResult(false, "failed", c.Target);
                    await _obs.SetSourceVisibleAsync(snap.CurrentScene, target, c.Kind == CommandKind.ShowSource);
                    break;
                case CommandKind.Mute: await _obs.SetMutedAsync(target, true); break;
                case CommandKind.Unmute: await _obs.SetMutedAsync(target, false); break;
                case CommandKind.VolumeUp: await _obs.AdjustVolumeDbAsync(target, _settings.VolumeStepDb); break;
                case CommandKind.VolumeDown: await _obs.AdjustVolumeDbAsync(target, -_settings.VolumeStepDb); break;
                case CommandKind.RecordStart: await _obs.RecordAsync(RecordAction.Start); break;
                case CommandKind.RecordStop: await _obs.RecordAsync(RecordAction.Stop); break;
                case CommandKind.RecordPause: await _obs.RecordAsync(RecordAction.Pause); break;
                case CommandKind.StreamStart: await _obs.StreamAsync(true); break;
                case CommandKind.StreamStop: await _obs.StreamAsync(false); break;
            }
            return new ExecResult(true, key, c.Target);
        }
        catch (Exception)
        {
            return new ExecResult(false, "failed", c.Target); // OBS fermé, source disparue...
        }
    }

    private static string ActionKey(CommandKind k) => k switch
    {
        CommandKind.SwitchScene => "scene",
        CommandKind.ShowSource => "show",
        CommandKind.HideSource => "hide",
        CommandKind.Mute => "mute",
        CommandKind.Unmute => "unmute",
        CommandKind.VolumeUp => "volumeUp",
        CommandKind.VolumeDown => "volumeDown",
        CommandKind.RecordStart => "recordStart",
        CommandKind.RecordStop => "recordStop",
        CommandKind.RecordPause => "recordPause",
        CommandKind.StreamStart => "streamStart",
        _ => "streamStop",
    };
}
